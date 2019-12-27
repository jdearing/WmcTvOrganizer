using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WmcTvOrganizer.Core.Models;

namespace WmcTvOrganizer.Core
{
    public class SeriesFinder
    {
        private readonly string _seriesUrl;
        private readonly string _updateUrl;
        private readonly string _episodeUrl;
        private readonly Regex _seasonEpisode;
        private readonly ILogger<SeriesFinder> _logger;
        private readonly ISettings _settings;
        private HttpClient _httpClient;

        public SeriesFinder(ISettings settings, IHttpClientFactory httpClientFactory, IOptions<SeriesFinderOptions> options, ILogger<SeriesFinder> logger,
            CancellationTokenSource cancellationTokenSource)
        {
            _settings = settings;
            _updateUrl = options.Value.UpdateUrl;
            _seriesUrl = options.Value.SeriesUrl;
            _episodeUrl = options.Value.EpisodeUrl;

            _httpClient = httpClientFactory.CreateClient(Program.TvDbApi);
            _logger = logger;

            _seasonEpisode = new Regex(@"s(\d+)e(\d+)\s(.+$)", RegexOptions.Compiled);
        }

        public async Task ProcessEpisodes(IEnumerable<WmcItem> wmcItems)
        {
            var updatedSeries = new HashSet<string>();
            _settings.TvDbLastUpdate = await GetLastDbUpdate(_updateUrl, _settings.TvDbLastUpdate, updatedSeries);
            await _settings.Save();

            var items = wmcItems as WmcItem[] ?? wmcItems.ToArray();
            foreach (var wmcItem in items)
            {
                if (!IgnoreWmcItem(wmcItem, _settings.IgnoreItems))
                {
                    if (wmcItem.Type == ItemType.Tv && !string.IsNullOrEmpty(wmcItem.Series.WmcName))
                    {
                        var series = FindKnownSeries(wmcItem, _settings.TvSeries);

                        if (series == null)
                        {
                            if (!UserIgnore(wmcItem, _settings.IgnoreItems))
                            {
                                series = await GetSeriesInfo(_seriesUrl, wmcItem, wmcItem.Series.WmcName);

                                if (series == null)
                                {
                                    var cont = await UserSearchSeries(wmcItem, _seriesUrl);
                                    while (cont.Item1)
                                    {
                                        cont = await UserSearchSeries(wmcItem, _seriesUrl);
                                    }

                                    series = cont.Item2;
                                }

                                if (series != null)
                                {
                                    _settings.TvSeries.Add(series);
                                }
                            }

                            if (series != null)
                            {
                                wmcItem.Series = series;
                            }
                        }
                        else
                        {
                            wmcItem.Series = series;
                        }

                        if (wmcItem.Series.TvDbName != null)
                        {
                            wmcItem.Series.FolderName = CleanFolderName(wmcItem.Series.TvDbName);

                            var match = _seasonEpisode.Match(wmcItem.Title);
                            if (match.Success)
                            {
                                wmcItem.TvDbEpisode = new TvDbEpisode
                                {
                                    SeasonNumber = Convert.ToInt32(match.Groups[1].Value),
                                    EpisodeNumber = Convert.ToInt32(match.Groups[2].Value),
                                    Name = match.Groups[3].Value
                                };
                            }
                        }
                    }
                    else if (wmcItem.Type == ItemType.Movie)
                    {
                        UserIgnore(wmcItem, _settings.IgnoreItems);
                    }
                }
            }

            // find the ones where we could not extract the season and episode from the title
            var distinctSeries = items.Where(file => file.Type == ItemType.Tv && file.TvDbEpisode == null).GroupBy(file => file.Series.TvDbId);

            foreach (var series in distinctSeries)
            {
                WmcItem episode = series.FirstOrDefault();

                if (episode?.Series.TvDbName != null)
                {
                    if (!IsKnownSeries(episode.Series.TvDbId, _settings.TvSeries) || updatedSeries.Contains(episode.Series.TvDbId))
                    {
                        await GetEpisodeData(_episodeUrl, episode, _settings.WorkingDirectory);
                    }
                }
            }


            await _settings.Save();
        }

        private async Task<Tuple<bool, TvSeries>> UserSearchSeries(WmcItem wmcItem, string seriesUrl)
        {
            var cont = true;
            Console.WriteLine("No series matches for {0}", wmcItem.Series.WmcName);

            Console.Write("Search for series (0 to skip): ");
            var s = Console.ReadLine();
            TvSeries series = null;
            if (int.TryParse(s, out var x) && x == 0)
            {
                cont = false;
            }
            else
            {
                series = await GetSeriesInfo(seriesUrl, wmcItem, s);
                if (series != null) cont = false;
            }

            return new Tuple<bool, TvSeries>(cont, series);
        }

        private bool IgnoreWmcItem(WmcItem wmcItem, List<string> ignoreItems)
        {
            var ignore = false;
            if (wmcItem.Type == ItemType.Tv)
            {
                ignore = ignoreItems.Contains(wmcItem.Series.WmcName);
            }
            else if (wmcItem.Type == ItemType.Movie)
            {
                ignore = ignoreItems.Contains(wmcItem.Title);
            }

            return ignore;
        }

        private bool UserIgnore(WmcItem wmcItem, List<string> ignoreItems)
        {
            if (wmcItem.Type == ItemType.Tv)
            {
                Console.Write("Ignore TV series {0} (y/n)? ", wmcItem.Series.WmcName);
            }
            else if (wmcItem.Type == ItemType.Movie)
            {
                Console.Write("Ignore Movie {0} ({1}) (y/n)? ", wmcItem.Title, wmcItem.ReleaseYear);
            }

            bool? ignore = null;

            while (!ignore.HasValue)
            {
                var s = Console.ReadLine();
                if (s.ToUpper() == "Y")
                {
                    ignore = true;
                }
                else if (s.ToUpper() == "N")
                {
                    ignore = false;
                }
            }

            if (ignore.Value)
            {
                if (wmcItem.Type == ItemType.Tv)
                {
                    ignoreItems.Add(wmcItem.Series.WmcName);
                }
                else if (wmcItem.Type == ItemType.Movie)
                {
                    ignoreItems.Add(wmcItem.Title);
                }
            }

            return ignore.Value;
        }

        private async Task GetEpisodeData(string episodeUrl, WmcItem episode, DirectoryInfo workingDirectory)
        {
            var url = string.Format(episodeUrl, episode.Series.TvDbId);

            episode.Series.FolderName = CleanFolderName(episode.Series.TvDbName);
            var path = Path.Combine(workingDirectory.FullName, episode.Series.FolderName);

            var di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                di.Create();
            }

            var file = await DownloadEpisodeZip(url, di);
            if (file != null)
            {
                episode.Series.EpisodeDataFile = file.FullName;
            }
        }

        public static string CleanFolderName(string folderName)
        {
            var invalids = new List<char> {':', '!', '?'};
            invalids.AddRange(Path.GetInvalidPathChars());
            return string.Join("_", folderName.Split(invalids.ToArray(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        private async Task<FileInfo> DownloadEpisodeZip(string url, DirectoryInfo workingDirectory)
        {
            var client = new HttpClient();
            FileInfo fi = null;
            try
            {
                var body = await client.GetByteArrayAsync(url);
                var path = Path.Combine(workingDirectory.FullName, "en.zip");
                fi = new FileInfo(path);
                using (var fs = new FileStream(fi.FullName, FileMode.Create, FileAccess.Write))
                {
                    await fs.WriteAsync(body, 0, body.Length);
                }

                var extractPath = Path.Combine(fi.DirectoryName, "en");
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                ZipFile.ExtractToDirectory(fi.FullName, extractPath);
                fi.Delete();
                fi = new FileInfo(Path.Combine(extractPath, "en.xml"));
                if (!fi.Exists)
                {
                    fi = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Message :{0} ", ex.Message);
                fi = null;
            }
            finally
            {
                client.Dispose();
            }

            return fi;
        }

        private TvSeries FindKnownSeries(WmcItem episode, IEnumerable<TvSeries> knownSeries)
        {
            var ts = (from s in knownSeries where s.WmcName == episode.Series.WmcName select s);
            var tvSeries = ts as IList<TvSeries> ?? ts.ToList();
            if (tvSeries.Any())
            {
                return tvSeries.First();
            }

            return null;
        }

        private bool IsKnownSeries(string tvDbId, List<TvSeries> knownSeries)
        {
            return (from s in knownSeries where s.TvDbId == tvDbId && s.EpisodeDataFile != null select s).Any();
        }

        private TvSeries UserSelectSeries(WmcItem episode, IList<TvSeries> tvSeries)
        {
            Console.WriteLine("Multiple series matches for {0}", episode.Series.WmcName);
            TvSeries tvs = null;
            for (var i = 0; i < tvSeries.Count; i++)
            {
                Console.WriteLine("{0}: {1}", i + 1, tvSeries[i].TvDbName);
            }

            while (tvs == null)
            {
                Console.Write("Select Series (0 to skip): ");
                var s = Console.ReadLine();
                if (int.TryParse(s, out var x))
                {
                    x--;
                    if (x == -1)
                    {
                        break;
                    }

                    if (x >= 0 && x < tvSeries.Count)
                    {
                        tvs = tvSeries[x];
                    }
                }
            }

            return tvs;
        }

        private async Task<TvSeries> GetSeriesInfo(string seriesUrl, WmcItem episode, string searchName)
        {
            TvSeries tvSeries = null;

            var url = string.Format(seriesUrl, WebUtility.UrlEncode(searchName));
            var xdoc = await GetXmlDoc(url);

            var series = (xdoc.Descendants("Series").Select
            (items => new TvSeries
            {
                WmcName = episode.Series.WmcName,
                TvDbId = items.Element("id")?.Value,
                TvDbName = items.Element("SeriesName")?.Value
            }));

            var tvs = series as IList<TvSeries> ?? series.ToList();
            if (tvs.Count == 1 && episode.Series.WmcName == tvs[0].TvDbName)
            {
                tvSeries = tvs[0];
            }
            else if (tvs.Count >= 1)
            {
                tvSeries = UserSelectSeries(episode, tvs);
            }

            return tvSeries;
        }

        private async Task<int> GetLastDbUpdate(string updateUrl, int last, HashSet<string> updateSeries)
        {
            var url = updateUrl;
            if (last == 0)
            {
                url = string.Format(updateUrl, "none", string.Empty);
            }
            else
            {
                url = string.Format(updateUrl, "series", last);
            }

            var lastUpdate = 0;
            var xdoc = await GetXmlDoc(url);
            if (xdoc != null)
            {
                var date = (from items in xdoc.Descendants("Items")
                    select items.Element("Time")?.Value).First();

                var updated = from el in xdoc.Descendants("Items").Elements("Series") select el.Value;

                foreach (var s in updated)
                {
                    updateSeries.Add(s);
                }

                int.TryParse(date, out lastUpdate);

            }

            return lastUpdate;
        }

        private async Task<XDocument> GetXmlDoc(string url)
        {
            XDocument xdoc = null;
            var client = new HttpClient();
            string responseBody = null;
            try
            {
                responseBody = await client.GetStringAsync(url);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error getting data from " + url);
            }
            finally
            {
                client.Dispose();
            }

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    xdoc = XDocument.Parse(responseBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing xml response");
                }
            }

            return xdoc;
        }
    }

    public class SeriesFinderOptions
    {
        public string UpdateUrl { get; set; }
        public string SeriesUrl { get; set; }
        public string EpisodeUrl { get; set; }
    }
}
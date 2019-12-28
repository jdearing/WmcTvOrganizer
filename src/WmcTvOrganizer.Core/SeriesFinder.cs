using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WmcTvOrganizer.Core.Models;

namespace WmcTvOrganizer.Core
{
    public class SeriesFinder : ISeriesFinder
    {
        private readonly Regex _seasonEpisode;
        private readonly ILogger<SeriesFinder> _logger;
        private readonly ISettings _settings;
        private readonly TvDbClient _tvDbClient;
        private CancellationToken _cancellationToken;

        public SeriesFinder(ISettings settings, TvDbClient tvDbClient, IOptions<SeriesFinderOptions> options, ILogger<SeriesFinder> logger,
            CancellationTokenSource cancellationTokenSource)
        {
            _settings = settings;
            _tvDbClient = tvDbClient;
            _logger = logger;
            _cancellationToken = cancellationTokenSource.Token;

            _seasonEpisode = new Regex(@"s(\d+)e(\d+)\s(.+$)", RegexOptions.Compiled);
        }

        public async Task ProcessEpisodes(IEnumerable<WmcItem> wmcItems)
        {
            var items = wmcItems as WmcItem[] ?? wmcItems.ToArray();
            foreach (var wmcItem in items)
            {
                if (!IgnoreWmcItem(wmcItem))
                {
                    if (wmcItem.Type == ItemType.Tv && !string.IsNullOrEmpty(wmcItem.Series.WmcName))
                    {
                        var series = FindKnownSeries(wmcItem);

                        if (series == null)
                        {
                            if (!UserIgnore(wmcItem))
                            {
                                series = await GetSeriesInfo(wmcItem, wmcItem.Series.WmcName);

                                if (series == null)
                                {
                                    //var cont = await UserSearchSeries(wmcItem, _seriesUrl);
                                    //while (cont.Item1)
                                    //{
                                    //    cont = await UserSearchSeries(wmcItem, _seriesUrl);
                                    //}

                                    //series = cont.Item2;
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
                        UserIgnore(wmcItem);
                    }
                }
            }

            // find the ones where we could not extract the season and episode from the title
            var distinctSeries = items.Where(file => file.Type == ItemType.Tv && file.TvDbEpisode == null)
                .GroupBy(file => file.Series.TvDbId);

            foreach (var series in distinctSeries)
            {
                WmcItem episode = series.FirstOrDefault();

                if (episode?.Series.TvDbName != null)
                {
                    if (!IsKnownSeries(episode.Series.TvDbId))
                    {
                        //await GetEpisodeData(episode, _settings.WorkingDirectory);
                    }
                }
            }


            await _settings.Save();
        }

        private bool IgnoreWmcItem(WmcItem wmcItem)
        {
            var ignore = false;

            if (wmcItem.Type == ItemType.Tv)
            {
                ignore = _settings.IgnoreItems.Contains(wmcItem.Series.WmcName);
            }
            else if (wmcItem.Type == ItemType.Movie)
            {
                ignore = _settings.IgnoreItems.Contains(wmcItem.Title);
            }

            return ignore;
        }

        private TvSeries FindKnownSeries(WmcItem episode)
        {
            foreach (var series in _settings.TvSeries)
            {
                if (series.WmcName == episode.Series.WmcName)
                {
                    return series;
                }
            }

            return null;
        }

        private bool UserIgnore(WmcItem wmcItem)
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
                if (s != null)
                {
                    if (s.ToUpper() == "Y")
                    {
                        ignore = true;
                    }
                    else if (s.ToUpper() == "N")
                    {
                        ignore = false;
                    }
                }
            }

            if (ignore.Value)
            {
                if (wmcItem.Type == ItemType.Tv)
                {
                    _settings.IgnoreItems.Add(wmcItem.Series.WmcName);
                }
                else if (wmcItem.Type == ItemType.Movie)
                {
                    _settings.IgnoreItems.Add(wmcItem.Title);
                }
            }

            return ignore.Value;
        }

        private async Task<TvSeries> GetSeriesInfo(WmcItem episode, string searchName)
        {
            TvSeries tvSeries = null;

            var doc = await _tvDbClient.SearchSeries(searchName);
            
            

            //IList<TvSeries> tvs = series as IList<TvSeries> ?? series.ToList();
            //if (tvs.Count == 1 && episode.Series.WmcName == tvs[0].TvDbName)
            //{
            //    tvSeries = tvs[0];
            //}
            //else if (tvs.Count >= 1)
            //{
            //    tvSeries = UserSelectSeries(episode, tvs);
            //}

            return tvSeries;
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
                //series = await GetSeriesInfo(seriesUrl, wmcItem, s);
                if (series != null) cont = false;
            }

            return new Tuple<bool, TvSeries>(cont, series);
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
            FileInfo fi;
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

        

        private bool IsKnownSeries(string tvDbId)
        {
            foreach (TvSeries s in _settings.TvSeries)
            {
                if (s.TvDbId == tvDbId) return true;
            }

            return false;
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
    }

    public class SeriesFinderOptions
    {
     
    }

    public interface ISeriesFinder
    {
        Task ProcessEpisodes(IEnumerable<WmcItem> wmcItems);
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

using log4net;

using WmcTvOrganizer.Common;
using WmcTvOrganizer.Model;

namespace WmcTvOrganizer.Process
{
    public class SeriesFinder
    {
        private readonly string _seriesUrl;
        private readonly string _updateUrl;
        private readonly string _episodeUrl;
        private readonly Settings _settings;
        private readonly ILog _logger;
        private readonly Regex _title;
        
        public SeriesFinder(Settings settings, string updateUrl, string seriesUrl, string episodeUrl, ILog logger)
        {
            _settings = settings;
            _updateUrl = updateUrl;
            _seriesUrl = seriesUrl;
            _episodeUrl = episodeUrl;
            _logger = logger;

            _title = new Regex(@"s(\d+)e(\d+)\s(.+$)");
        }

        public async Task ProcessEpisodes(List<WmcItem> wmcItems)
        {
            HashSet<string> updatedSeries = new HashSet<string>();
            _settings.TvDbLastUpdate = await GetLastDbUpdate(_updateUrl, _settings.TvDbLastUpdate, updatedSeries, _logger);
            await _settings.Save(_logger);

            foreach (WmcItem wmcItem in wmcItems)
            {
                if (!IgnoreWmcItem(wmcItem, _settings.IgnoreItems))
                {
                    if (wmcItem.Type == ItemType.Tv && !string.IsNullOrEmpty(wmcItem.Series.WmcName))
                    {
                        TvSeries series = FindKnownSeries(wmcItem, _settings.TvSeries);

                        if (series == null)
                        {
                            if (!UserIgnore(wmcItem, _settings.IgnoreItems))
                            {
                                series = await GetSeriesInfo(_seriesUrl, wmcItem, wmcItem.Series.WmcName, _logger);
 
                                if (series == null)
                                {
                                    Tuple<bool, TvSeries> cont = await UserSearchSeries(wmcItem, _seriesUrl, _logger);
                                    while (cont.Item1)
                                    {
                                        cont = await UserSearchSeries(wmcItem, _seriesUrl, _logger);
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

                        Match match = _title.Match(wmcItem.Title);
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
                    else if (wmcItem.Type == ItemType.Movie)
                    {
                        UserIgnore(wmcItem, _settings.IgnoreItems);
                    }
                }
            }

            IEnumerable<IGrouping<string, WmcItem>> distinctSeries = wmcItems.Where(file => file.Type == ItemType.Tv).GroupBy(file => file.Series.TvDbId);
            
            await distinctSeries.ForEachAsync(5, async (item) =>
                {
                    WmcItem episode = item.FirstOrDefault();

                    if (episode != null && episode.Series.TvDbName != null)
                    {
                        if (!IsKnownSeries(episode.Series.TvDbId, _settings.TvSeries) || updatedSeries.Contains(episode.Series.TvDbId))
                        {
                            await GetEpisodeData(_episodeUrl, episode, _settings.WorkingDirectory);
                        }   
                    }
                });
 
            await _settings.Save(_logger);
        }

        private async Task<Tuple<bool, TvSeries>> UserSearchSeries(WmcItem wmcItem, string seriesUrl, ILog logger)
        {
            bool cont = true;
            Console.WriteLine("No series matches for {0}", wmcItem.Series.WmcName);

            Console.Write("Search for series (0 to skip): ");
            string s = Console.ReadLine();
            TvSeries series = null;
            if (int.TryParse(s, out int x) && x == 0)
            {
                cont = false;
            }
            else
            {
                series = await GetSeriesInfo(seriesUrl, wmcItem, s, logger);
                if (series != null) cont = false;
            }

            return new Tuple<bool,TvSeries>(cont, series);
        }

        private bool IgnoreWmcItem(WmcItem wmcItem, List<string> ignoreItems)
        {
            bool ignore = false;
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
                string s = Console.ReadLine();
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
            string url = string.Format(episodeUrl, episode.Series.TvDbId);

            episode.Series.FolderName = CleanFolderName(episode.Series.TvDbName);
            string path = Path.Combine(workingDirectory.FullName, episode.Series.FolderName);

            DirectoryInfo di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                di.Create();
            }

            FileInfo file = await DownloadEpisodeZip(url, di);
            if (file != null)
            {
                episode.Series.EpisodeDataFile = file.FullName;
            }
        }

        public static string CleanFolderName(string folderName)
        {
            List<char> invalids = new List<char> {':', '!'};
            invalids.AddRange(Path.GetInvalidPathChars());
            return string.Join("_", folderName.Split(invalids.ToArray(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        private async Task<FileInfo> DownloadEpisodeZip(string url, DirectoryInfo workingDirectory)
        {
            HttpClient client = new HttpClient();
            FileInfo fi = null;
            try
            {
                byte[] body = await client.GetByteArrayAsync(url);
                string path = Path.Combine(workingDirectory.FullName, "en.zip");
                fi = new FileInfo(path);
                using (FileStream fs = new FileStream(fi.FullName, FileMode.Create, FileAccess.Write))
                {
                    await fs.WriteAsync(body, 0, body.Length);
                }

                string extractPath = Path.Combine(fi.DirectoryName, "en");
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
            IEnumerable<TvSeries> ts = (from s in knownSeries where s.WmcName == episode.Series.WmcName select s);
            IList<TvSeries> tvSeries = ts as IList<TvSeries> ?? ts.ToList();
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
            for (int i = 0; i < tvSeries.Count; i++)
            {
                Console.WriteLine("{0}: {1}", i + 1, tvSeries[i].TvDbName);
            }

            while (tvs == null)
            {
                Console.Write("Select Series (0 to skip): ");
                string s = Console.ReadLine();
                if (int.TryParse(s, out int x))
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

        private async Task<TvSeries> GetSeriesInfo(string seriesUrl, WmcItem episode, string searchName, ILog logger)
        {
            TvSeries tvSeries = null;

            string url = string.Format(seriesUrl, WebUtility.UrlEncode(searchName));
            XDocument xdoc = await GetXmlDoc(url, logger);

            IEnumerable<TvSeries> series = (xdoc.Descendants("Series").Select
                (items => new TvSeries
                    {
                        WmcName = episode.Series.WmcName,
                        TvDbId = items.Element("id")?.Value,
                        TvDbName = items.Element("SeriesName")?.Value
                        }));

            IList<TvSeries> tvs = series as IList<TvSeries> ?? series.ToList();
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

        private async Task<int> GetLastDbUpdate(string updateUrl, int last, HashSet<string> updateSeries, ILog logger)
        {
            string url = updateUrl;
            if (last == 0)
            {
                url = string.Format(updateUrl, "none", string.Empty);
            }
            else
            {
                url = string.Format(updateUrl, "series", last);
            }

            int lastUpdate = 0;
            XDocument xdoc = await GetXmlDoc(url, logger);
            if (xdoc != null)
            {
                string date = (from items in xdoc.Descendants("Items")
                               select items.Element("Time")?.Value).First();
                
                IEnumerable<string> updated = from el in xdoc.Descendants("Items").Elements("Series") select el.Value; 
                
                foreach (string s in updated)
                {
                    updateSeries.Add(s);
                }
                
                int.TryParse(date, out lastUpdate);
                                  
            }
            return lastUpdate;
        }

        private async Task<XDocument> GetXmlDoc(string url, ILog logger)
        {
            XDocument xdoc = null;
            HttpClient client = new HttpClient();
            string responseBody = null;
            try
            {
                responseBody = await client.GetStringAsync(url);
            }
            catch (HttpRequestException ex)
            {
                logger.Error("Error getting data from " + url, ex);
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
                catch(Exception ex)
                {
                    logger.Error("Error parsing xml response", ex);
                }
            }

            return xdoc;
        }
    }
}

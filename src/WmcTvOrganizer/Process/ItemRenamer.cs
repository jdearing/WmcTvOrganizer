using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using log4net;

using WmcTvOrganizer.Common;
using WmcTvOrganizer.Model;

namespace WmcTvOrganizer.Process
{
    public class ItemRenamer
    {
        private readonly string _destinationTvFolder;
        private readonly string _destinationProtectedTvFolder;
        private readonly string _destinationMovieFolder;
        private readonly string _destinationProtectedMovieFolder;
        private readonly ILog _logger;
        
        public ItemRenamer(string destinationTvFolder, string destinationProtectedTvFolder, string destinationMovieFolder, string destinationProtectedMovieFolder, ILog logger)
        {
            _destinationTvFolder = destinationTvFolder;
            _destinationProtectedTvFolder = destinationProtectedTvFolder;
            _destinationMovieFolder = destinationMovieFolder;
            _destinationProtectedMovieFolder = destinationProtectedMovieFolder;
            _logger = logger;
        }

        public void ProcessEpisodes(List<WmcItem> wmcItems)
        {
            foreach (WmcItem wmcItem in wmcItems)
            {
                if (wmcItem.Type == ItemType.Tv && wmcItem.Series != null)
                {
                    try
                    {
                        GetEpisodeDetails(wmcItem);
                    }
                    catch(Exception ex)
                    {
                        _logger.Error("Error getting episode details", ex);
                    }

                    if (wmcItem.TvDbEpisode != null)
                    {
                        RenameTvFile(_destinationTvFolder, wmcItem, _logger);
                    }
                }
                else if (wmcItem.Type == ItemType.Movie)
                {
                    RenameMovieFile(_destinationMovieFolder, wmcItem, _logger);
                }
            }
        }

        private void GetEpisodeDetails(WmcItem episode)
        {
            if (episode.Series.EpisodeDataFile != null)
            {

                XDocument xdoc = XDocument.Load(episode.Series.EpisodeDataFile);
                XElement data = xdoc.Element("Data");

                int epNum, seaNum;
                DateTime fa;

                IEnumerable<TvDbEpisode> tvEps = from eps in data.Elements("Episode")
                            where eps.Element("EpisodeName").Value == episode.Title
                            select new TvDbEpisode
                            {
                                Name = eps.Element("EpisodeName").Value,
                                EpisodeNumber = (int.TryParse(eps.Element("EpisodeNumber").Value, out epNum) ? epNum : 0),
                                SeasonNumber = (int.TryParse(eps.Element("SeasonNumber").Value, out seaNum) ? seaNum : 0),
                                FirstAired = (DateTime.TryParse(eps.Element("FirstAired").Value, Config.EnUkCulture, DateTimeStyles.AdjustToUniversal, out fa) ? fa : DateTime.MinValue)
                            };



                if (!tvEps.Any())
                {
                    tvEps = from eps in data.Elements("Episode")
                            where eps.Element("Overview").Value == episode.Description
                            select new TvDbEpisode
                            {
                                Name = eps.Element("EpisodeName").Value,
                                EpisodeNumber = (int.TryParse(eps.Element("EpisodeNumber").Value, out epNum) ? epNum : 0),
                                SeasonNumber = (int.TryParse(eps.Element("SeasonNumber").Value, out seaNum) ? seaNum : 0),
                                FirstAired = (DateTime.TryParse(eps.Element("FirstAired").Value, Config.EnUkCulture, DateTimeStyles.AdjustToUniversal, out fa) ? fa : DateTime.MinValue)
                            };
                }

                if (!tvEps.Any())
                {
                    tvEps = from eps in data.Elements("Episode")
                            where (DateTime.TryParse(eps.Element("FirstAired").Value, Config.EnUkCulture, DateTimeStyles.AdjustToUniversal, out fa) ? fa : DateTime.MinValue) == episode.BroadcastDate
                            select new TvDbEpisode
                            {
                                Name = eps.Element("EpisodeName").Value,
                                EpisodeNumber = (int.TryParse(eps.Element("EpisodeNumber").Value, out epNum) ? epNum : 0),
                                SeasonNumber = (int.TryParse(eps.Element("SeasonNumber").Value, out seaNum) ? seaNum : 0),
                                FirstAired = (DateTime.TryParse(eps.Element("FirstAired").Value, Config.EnUkCulture, DateTimeStyles.AdjustToUniversal, out fa) ? fa : DateTime.MinValue)
                            };
                }

                IList<TvDbEpisode> tvDbEpisodes = tvEps as IList<TvDbEpisode> ?? tvEps.ToList();
                if (tvDbEpisodes.Count() == 1)
                {
                    episode.TvDbEpisode = tvDbEpisodes.First();
                }
                else if (tvDbEpisodes.Count() > 1)
                {
                    IEnumerable<TvDbEpisode> dateMatch = tvDbEpisodes.Where(ep => ep.FirstAired == episode.BroadcastDate);
                    if (dateMatch.Count() == 1)
                    {
                        episode.TvDbEpisode = dateMatch.First();
                    }
                    else 
                    {
                        episode.TvDbEpisode = UserSelectEpisode(episode, tvDbEpisodes);
                    }
                }
            }
        }

        private TvDbEpisode UserSelectEpisode(WmcItem episode, IList<TvDbEpisode> tvDbEpisodes)
        {
            Console.WriteLine("Multiple episode match for {0}: {1} {2}", episode.Series.TvDbName, episode.Title, episode.BroadcastDate);
            int x = 0;
            TvDbEpisode tvs = null;
            for (int i = 0; i < tvDbEpisodes.Count; i++)
            {
                Console.WriteLine("{0}: s{1} e{2} {3} {4}", i + 1, tvDbEpisodes[i].SeasonNumber, tvDbEpisodes[i].EpisodeNumber, tvDbEpisodes[i].Name, tvDbEpisodes[i].FirstAired);
            }

            while (tvs == null)
            {
                Console.Write("Select Episode (0 to skip): ");
                string s = Console.ReadLine();
                if (int.TryParse(s, out x))
                {
                    x--;
                    if (x == -1)
                    {
                        break;
                    }
                    else if (x >= 0 && x < tvDbEpisodes.Count)
                    {
                        tvs = tvDbEpisodes[x];
                    }
                }
            }

            return tvs;
        }

        //Shows/Show_Name/Season XX/ShowName - sXXeYY - Optional_Info.ext

        private const string FileNameFormat = "{0} - s{1}e{2} - {3}.wtv";

        private void RenameTvFile(string destinationFolder, WmcItem episode, ILog logger)
        {

            string path = Path.Combine(destinationFolder, episode.Series.FolderName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path = Path.Combine(path, "Season " + episode.TvDbEpisode.SeasonNumber.ToString().PadLeft(2, '0'));
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            string fileName = string.Format(FileNameFormat, episode.Series.FolderName,E:\SourceCode\github\WmcTvOrganizer\src\WmcTvOrganizer\Process\ItemRenamer.cs
                episode.TvDbEpisode.SeasonNumber.ToString().PadLeft(2, '0'),
                episode.TvDbEpisode.EpisodeNumber.ToString().PadLeft(2, '0'),
                CleanFileName(episode.TvDbEpisode.Name));


            path = Path.Combine(path, fileName);

            MoveFile(episode.File.FullName, path, logger);
        }

        public static string CleanFileName(string fileName)
        {
            List<char> invalids = new List<char> { '\\', '/', ':' };
            invalids.AddRange(Path.GetInvalidPathChars());
            return string.Join("_", fileName.Split(invalids.ToArray(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        private void RenameMovieFile(string destinationMovieFolder, WmcItem wmcItem, ILog logger)
        {
            string path = destinationMovieFolder;
            if (!Directory.Exists(destinationMovieFolder))
            {
                Directory.CreateDirectory(destinationMovieFolder);
            }

            string fileName = wmcItem.File.Name; 
            int index = wmcItem.File.Name.IndexOf('_');
            if (index > 0)
            {
                fileName = fileName.Substring(0, index);
            }

            fileName = fileName + " (" + wmcItem.ReleaseYear + ")" + wmcItem.File.Extension;

            path = Path.Combine(path, fileName);

            MoveFile(wmcItem.File.FullName, path, logger);
        }

        private void MoveFile(string from, string to, ILog logger)
        {
            
            logger.InfoFormat("Moving {0} to {1}", from, to);
            try
            {
                //File.Move(from, to);
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("Error Moving {0} to {1} {2}", from, to, ex);
            }
        }
    }
}

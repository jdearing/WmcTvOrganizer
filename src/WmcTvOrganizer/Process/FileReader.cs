using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Shell32;

using log4net;

using WmcTvOrganizer.Common;
using WmcTvOrganizer.Model;

namespace WmcTvOrganizer.Process
{
    public class FileReader
    {
        private readonly DirectoryInfo _recordedTvDirectory;
        private readonly Dictionary<string, int> _attributeIndexes;
        private readonly ILog _logger;

        public FileReader(string recordedTvPath, ILog logger)
        {
            _recordedTvDirectory = new DirectoryInfo(recordedTvPath);
            _attributeIndexes = new Dictionary<string, int>
            {
                {"File extension", -1},
                {"Title", -1},
                {"Subtitle", -1},
                {"Path", -1},
                {"Episode name", -1},
                {"Broadcast date", -1},
                {"Program description", -1},
                {"Rerun", -1},
                {"Date released", -1},
                {"Genre", -1},
                {"Protected", -1}
            };
            _logger = logger;
        }

        public List<WmcItem> FindFiles()
        {
            _logger.InfoFormat("Searching directory: {0}", _recordedTvDirectory);
            List<WmcItem> wmcItems = GetEpisodes(_recordedTvDirectory);

            foreach (WmcItem episode in wmcItems)
            {
                if (episode.File != null)
                {
                    _logger.InfoFormat("Found File: {0} Series {1} Title {2}", episode.File.FullName, episode.Series, episode.Title);
                }
            }

            return wmcItems;
        }
        
        private List<WmcItem> GetEpisodes(DirectoryInfo tvDirectory)
        {
            Folder folder = GetShell32NameSpaceFolder(tvDirectory.FullName);

            GetAttributeIndexes(folder, _attributeIndexes);
            
            List<WmcItem> wmcItems = new List<WmcItem>();

            foreach (FolderItem2 item in folder.Items())
            {
                string attrValue = folder.GetDetailsOf(item, _attributeIndexes["File extension"]);
                if (attrValue.ToLower() == ".wtv")
                {

                    //for (int i = 0; i < 500; i++)
                    //{
                    //    string name = folder.GetDetailsOf(null, i);
                    //    string value = folder.GetDetailsOf(item, i);
                    //    if (name.Length > 0 || value.Length > 0)
                    //    {
                    //        _logger.Debug(name + ": " + value);
                    //    }
                    //}

                    WmcItem wmcItem = new WmcItem();

                    attrValue = folder.GetDetailsOf(item, _attributeIndexes["Path"]);
                    if (!string.IsNullOrWhiteSpace(attrValue) && File.Exists(attrValue))
                    {
                        wmcItem.File = new FileInfo(attrValue);
                    }

                    string title = folder.GetDetailsOf(item, _attributeIndexes["Title"]);

                    string epName = folder.GetDetailsOf(item, _attributeIndexes["Subtitle"]);

                    if (string.IsNullOrWhiteSpace(epName))
                    {
                        epName = folder.GetDetailsOf(item, _attributeIndexes["Episode name"]);
                        if (string.IsNullOrWhiteSpace(wmcItem.Title))
                        {
                            epName = string.Empty;
                        }
                    }

                    attrValue = folder.GetDetailsOf(item, _attributeIndexes["Broadcast date"]);
                    attrValue = CleanString(attrValue, (char)8206);
                    attrValue = CleanString(attrValue, (char)8207);
                    if (!string.IsNullOrWhiteSpace(attrValue))
                    {
                        if (DateTime.TryParse(attrValue, Config.EnUsCulture, DateTimeStyles.AdjustToUniversal, out DateTime dt))
                        {
                            wmcItem.BroadcastDate = dt;
                        }
                    }

                    wmcItem.Protected = folder.GetDetailsOf(item, _attributeIndexes["Protected"]).ToLower() == "yes";
                    
                    string genre = folder.GetDetailsOf(item, _attributeIndexes["Genre"]);
                    bool isMovie = genre.ToLower().Contains("movie");
                    attrValue = folder.GetDetailsOf(item, _attributeIndexes["Date released"]);
                    if (!int.TryParse(attrValue, out int year))
                    {
                        year = int.MinValue;
                    }
                    
                    if (isMovie || epName == string.Empty && year > 1900)
                    {
                        if (year > 0)
                        {
                            wmcItem.ReleaseYear = year;
                        }
                        else
                        {
                            wmcItem.ReleaseYear = wmcItem.BroadcastDate.Year;
                        }
                        
                        wmcItem.Type = ItemType.Movie;
                        wmcItem.Title = title;
                    }
                    else
                    {
                        wmcItem.Type = ItemType.Tv;
                        wmcItem.Series = new TvSeries { WmcName = title };
                        wmcItem.Title = epName;                       
                        wmcItem.Description = folder.GetDetailsOf(item, _attributeIndexes["Program description"]);
                        wmcItem.IsReRun = folder.GetDetailsOf(item, _attributeIndexes["Rerun"]).ToLower() == "yes";
                    }

                    wmcItems.Add(wmcItem);
                }
            }

            return wmcItems;
        }

        private void GetAttributeIndexes(Folder folder, Dictionary<string, int> attributeIndexes)
        {
            for (int i = 0; i < 500; i++)
            {
                string attribute = folder.GetDetailsOf(null, i);
                if (attributeIndexes.ContainsKey(attribute))
                {
                    attributeIndexes[attribute] = i;
                }
            }
        }

        private string CleanString(string s, char c)
        {
            int index = s.IndexOf(c);
            while (index >= 0)
            {
                s = s.Remove(index, 1);
                index = s.IndexOf(c);
            }
            return s;
        }

        private Folder GetShell32NameSpaceFolder(Object folder)
        {
            Type shellAppType = Type.GetTypeFromProgID("Shell.Application");

            Object shell = Activator.CreateInstance(shellAppType);
            return (Folder)shellAppType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { folder });
        } 
    }   
}

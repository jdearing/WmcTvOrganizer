using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shell32;

using WmcTvOrganizer.Core.Models;

namespace WmcTvOrganizer.Core
{
    public class FileReader : IFileReader
    {
        private readonly DirectoryInfo _recordedTvDirectory;
        private readonly Dictionary<string, int> _attributeIndexes;
        private readonly ILogger _logger;

        public FileReader(IOptions<FileReaderOptions> options, ILogger<FileReader> logger, CancellationTokenSource cancellationTokenSource)
        {
            _recordedTvDirectory = new DirectoryInfo(options.Value.RecordedTvDirectory);
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

        public IEnumerable<WmcItem> FindFiles()
        {
            _logger.LogInformation($"Searching directory: {_recordedTvDirectory}");
            var wmcItems = GetEpisodes(_recordedTvDirectory);

            var files = wmcItems as WmcItem[] ?? wmcItems.ToArray();
            foreach (var episode in files)
            {
                if (episode.File != null)
                {
                    _logger.LogInformation($"Found file: {episode.File.FullName} Series: {episode.Series.WmcName} Title: {episode.Title}");
                }
            }

            return files;
        }

        private IEnumerable<WmcItem> GetEpisodes(DirectoryInfo tvDirectory)
        {
            var folder = GetShell32NameSpaceFolder(tvDirectory.FullName);

            GetAttributeIndexes(folder, _attributeIndexes);

            var wmcItems = new List<WmcItem>();

            foreach (FolderItem2 item in folder.Items())
            {
                var attrValue = folder.GetDetailsOf(item, _attributeIndexes["File extension"]);
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

                    var wmcItem = new WmcItem();

                    attrValue = folder.GetDetailsOf(item, _attributeIndexes["Path"]);
                    if (!string.IsNullOrWhiteSpace(attrValue) && File.Exists(attrValue))
                    {
                        wmcItem.File = new FileInfo(attrValue);
                    }

                    var title = folder.GetDetailsOf(item, _attributeIndexes["Title"]);

                    var epName = folder.GetDetailsOf(item, _attributeIndexes["Subtitle"]);

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
                        if (DateTime.TryParse(attrValue, Program.EnUsCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                        {
                            wmcItem.BroadcastDate = dt;
                        }
                    }

                    wmcItem.Protected = folder.GetDetailsOf(item, _attributeIndexes["Protected"]).ToLower() == "yes";

                    var genre = folder.GetDetailsOf(item, _attributeIndexes["Genre"]);
                    var isMovie = genre.ToLower().Contains("movie");
                    attrValue = folder.GetDetailsOf(item, _attributeIndexes["Date released"]);
                    if (!int.TryParse(attrValue, out var year))
                    {
                        year = int.MinValue;
                    }

                    if (isMovie || epName == string.Empty && year > 1900)
                    {
                        wmcItem.ReleaseYear = year > 0 ? year : wmcItem.BroadcastDate.Year;

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
            for (var i = 0; i < 500; i++)
            {
                var attribute = folder.GetDetailsOf(null, i);
                if (attributeIndexes.ContainsKey(attribute))
                {
                    attributeIndexes[attribute] = i;
                }
            }
        }

        private string CleanString(string s, char c)
        {
            var index = s.IndexOf(c);
            while (index >= 0)
            {
                s = s.Remove(index, 1);
                index = s.IndexOf(c);
            }
            return s;
        }

        private Folder GetShell32NameSpaceFolder(Object folder)
        {
            var shellAppType = Type.GetTypeFromProgID("Shell.Application");

            var shell = Activator.CreateInstance(shellAppType);
            return (Folder)shellAppType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { folder });
        }
    }

    public class FileReaderOptions
    {
        public string RecordedTvDirectory { get; set; }
    }

    public interface IFileReader
    {
        IEnumerable<WmcItem> FindFiles();
    }

}

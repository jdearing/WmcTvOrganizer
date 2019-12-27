using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using WmcTvOrganizer.Core.Models;


namespace WmcTvOrganizer.Core
{
    public class Settings : ISettings
    {
        private const string FileName = "Settings.json";
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger<Settings> _logger;
        private readonly FileInfo _file;

        public Settings(ILogger<Settings> logger, CancellationTokenSource cancellationTokenSource)
        {
            _logger = logger;
            _cancellationTokenSource = cancellationTokenSource;

            TvDbLastUpdate = 0;
            TvSeries = new List<TvSeries>();
            IgnoreItems = new List<string>();
            var assemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title;
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle);
            var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle, FileName);
            _file = new FileInfo(fileName);
            WorkingDirectory = new DirectoryInfo(folder);
            if (!WorkingDirectory.Exists)
            {
                WorkingDirectory.Create();
            }
        }

        public int TvDbLastUpdate { get; set; }

        public List<TvSeries> TvSeries { get; set; }

        public List<string> IgnoreItems { get; set; }

        public DirectoryInfo WorkingDirectory { get; set; }
    
        public async Task Save()
        {
            var s = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            try
            {
                if (_file.Directory != null && !_file.Directory.Exists)
                {
                    _file.Directory.Create();
                }

                using (var sw = new StreamWriter(_file.FullName))
                {
                    await sw.WriteAsync(s);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
            }
        }

        public async Task Load()
        {
            if (_file.Exists)
            {
                try
                {
                    using (var sr = _file.OpenText())
                    {
                        var s = await sr.ReadToEndAsync();
                        var settings = JsonConvert.DeserializeObject<Settings>(s);
                        TvDbLastUpdate = settings.TvDbLastUpdate;
                        TvSeries = settings.TvSeries;
                        WorkingDirectory = settings.WorkingDirectory;
                        IgnoreItems = settings.IgnoreItems;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving settings");
                }
            }
        }
    }

    public interface ISettings
    {
        Task Load();
        Task Save();
        int TvDbLastUpdate { get; set; }
        List<TvSeries> TvSeries { get; set; }
        List<string> IgnoreItems { get; set; }
        DirectoryInfo WorkingDirectory { get; set; }
    }
}

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
        private DirectoryInfo _workingFolder;

        public Settings(ILogger<Settings> logger, CancellationTokenSource cancellationTokenSource)
        {
            _logger = logger;
            _cancellationTokenSource = cancellationTokenSource;

            TvSeries = new List<TvSeries>();
            IgnoreItems = new List<string>();
            var assemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title;
            WorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle);
            var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle, FileName);
            _file = new FileInfo(fileName);
            _workingFolder = new DirectoryInfo(WorkingDirectory);
            if (!_workingFolder.Exists)
            {
                _workingFolder.Create();
            }
        }

        
        public IList<TvSeries> TvSeries { get; private set; }

        public IList<string> IgnoreItems { get; private set; }

        public string WorkingDirectory { get; private set; }
    
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
        IList<TvSeries> TvSeries { get; }
        IList<string> IgnoreItems { get;  }
        string WorkingDirectory { get; }
    }
}

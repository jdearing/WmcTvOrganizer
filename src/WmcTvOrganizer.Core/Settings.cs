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
    public class Settings
    {
        private const string FileName = "Settings.json";
        private readonly string _settingFileName;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger<Settings> _logger;

        private Settings(ILogger<Settings> logger, CancellationTokenSource cancellationTokenSource)
        {
            _logger = logger;
            _cancellationTokenSource = cancellationTokenSource;

            TvDbLastUpdate = 0;
            TvSeries = new List<TvSeries>();
            IgnoreItems = new List<string>();
            string assemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle);
            _settingFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle, FileName);
            WorkingDirectory = new DirectoryInfo(folder);
            if (!WorkingDirectory.Exists)
            {
                WorkingDirectory.Create();
            }
        }

        public int TvDbLastUpdate { get; set; }

        public List<TvSeries> TvSeries { get; set; }

        public List<string> IgnoreItems { get; set; }

        public DirectoryInfo WorkingDirectory
        {
            get; private set;
        }

        public async Task Save()
        {
            string s = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            try
            {
                FileInfo fi = new FileInfo(_settingFileName);
                if (fi.Directory != null && !fi.Directory.Exists)
                {
                    fi.Directory.Create();
                }
                using (StreamWriter sw = new StreamWriter(_settingFileName))
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
            Settings us = null;

            if (File.Exists(_settingFileName))
            {
                try
                {
                    using (StreamReader sr = new StreamReader(_settingFileName))
                    {
                        string s = await sr.ReadToEndAsync();
                        this = JsonConvert.DeserializeObject<Settings>(s);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving settings");
                }
            }
        }
    }
}

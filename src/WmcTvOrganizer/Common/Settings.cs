using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using WmcTvOrganizer.Model;

namespace WmcTvOrganizer.Common
{
    public class Settings
    {
        // setup the singelton
        private static volatile Settings _instance;
        private static object _syncRoot;
        private const string FileName = "Settings.js";
        private static readonly string SettingFileName;
        static Settings()
        {
            string assemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title;
            SettingFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle, FileName);
            _syncRoot = new object();
            _instance = null;
            
        }

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                            _instance = new Settings();
                    }
                }

                return _instance;
            }
            private set
            {
                lock (_syncRoot)
                {
                    _instance = value;
                }
            }
        }

        private Settings()
        {
            TvDbLastUpdate = 0;
            TvSeries = new List<TvSeries>();
            IgnoreItems = new List<string>();
            string assemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle);
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

        public async Task Save(ILog logger)
        {
            string s = JsonConvert.SerializeObject(this, Formatting.Indented);
            try
            {
                FileInfo fi = new FileInfo(SettingFileName);
                if (!fi.Directory.Exists)
                {
                    fi.Directory.Create();
                }
                using (StreamWriter sw = new StreamWriter(SettingFileName))
                {
                    await sw.WriteAsync(s);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error saving settings", ex);
            }
        }

        public async Task Load(ILog logger)
        {
            Settings us = null;
            
            if (File.Exists(SettingFileName))
            {
                try
                {
                    using (StreamReader sr = new StreamReader(SettingFileName))
                    {
                        string s = await sr.ReadToEndAsync();
                        us = JsonConvert.DeserializeObject<Settings>(s);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Error loading settings", ex);
                }

                if (us != null)
                {
                    Instance = us;
                }
            }
        }
    }

}

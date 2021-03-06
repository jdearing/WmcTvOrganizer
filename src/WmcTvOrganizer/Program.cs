﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using WmcTvOrganizer.Common;
using WmcTvOrganizer.Model;
using WmcTvOrganizer.Process;

namespace WmcTvOrganizer
{
    public class Program
    {
        private static Program _program = null;
        private static ILog _logger = null;

        private static void Main(string[] args)
        {
            bool interactive = false;
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                interactive = args[0] == "interactive";
            }

            interactive = true;

            Logger logger = new Logger(Config.Get<string>("LogFileName", "Log.txt"), Config.Get("LogFileMaxSizeKB", 1024));
            if (interactive)
            {
                logger.AddConsoleAppender();
            }

            _logger = logger.Log;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += OnUnhandledException;

            _program = new Program(interactive, _logger);
            

            Task.Run(async () =>
            {
                await _program.Begin();
            }).Wait();

            if (interactive)
            {
                Console.WriteLine("Press \'q\' to quit the program.");
                while (Console.Read() != 'q') { }
            }

            Stop();
        }


        public static void Stop()
        {
            if (_program != null)
                _program.End();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception ex = (Exception)args.ExceptionObject;            
            if (_logger != null)
            {
                _logger.Fatal("Unhandled exception", ex);
            }
            Stop();
        }

        private ILog _log;
        private bool _interactive;
        private Program(bool interactive, ILog logger)
        {
            _interactive = interactive;
            _log = logger;   
        }

        private async Task Begin()
        {
            _logger.Info("Starting");
            await Settings.Instance.Load(_log);
            Settings settings = Settings.Instance;

            FileReader fileReader = new FileReader(Config.Get<string>("RecordedTvPath"), _log);
            List<WmcItem> wmcItems = fileReader.FindFiles();
            if (wmcItems != null && wmcItems.Count > 0)
            {
                SeriesFinder finder = new SeriesFinder(settings, Config.Get<string>("UpdateUrl"), Config.Get<string>("SeriesUrl"), Config.Get<string>("EpisodeUrl"), _log);
                await finder.ProcessEpisodes(wmcItems);
                ItemRenamer renamer = new ItemRenamer(Config.Get<string>("DestinationTvPath"), Config.Get<string>("DestinationProtectedTvPath"),
                    Config.Get<string>("DestinationMoviePath"), Config.Get<string>("DestinationProtectedMoviePath"), _log);
                renamer.ProcessEpisodes(wmcItems);
            }

            RemoveEmptyFolders(new DirectoryInfo(Config.Get<string>("DestinationTvPath")), 0);
            RemoveEmptyFolders(new DirectoryInfo(Config.Get<string>("DestinationProtectedTvPath")), 0);
        }

        private void RemoveEmptyFolders(DirectoryInfo directory, int depth)
        {
            if (directory.Exists)
            {
                foreach (var subDirectory in directory.EnumerateDirectories())
                {
                    RemoveEmptyFolders(subDirectory, depth + 1);
                }

                if (depth > 0)
                {
                    var infos = directory.EnumerateFileSystemInfos();
                    if (!infos.Any())
                    {
                        _log.Info($"Removing folder {directory.FullName}");
                        try
                        {
                            directory.Delete();
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Error removing folder {directory.FullName}", ex);
                        }
                    }
                }
            }
        }

        private void End()
        {
        
        }
    }
}

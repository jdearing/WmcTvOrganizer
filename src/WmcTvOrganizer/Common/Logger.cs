using System;
using System.IO;
using System.Reflection;

using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace WmcTvOrganizer.Common
{
    public class Logger
    {
        private PatternLayout _layout = new PatternLayout();
        private const string LogPattern = "%d %-5p %m%n";
        
        public Logger(string fileName, int maxSizeKB)
        {
            _layout.ConversionPattern = LogPattern;
            _layout.ActivateOptions();

            string assemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title;
            fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), assemblyTitle, fileName);

            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            PatternLayout patternLayout = new PatternLayout { ConversionPattern = LogPattern };
            patternLayout.ActivateOptions();

            RollingFileAppender roller = new RollingFileAppender
            {
                Layout = patternLayout,
                AppendToFile = true,
                RollingStyle = RollingFileAppender.RollingMode.Size,
                MaxSizeRollBackups = 4,
                MaximumFileSize = maxSizeKB + "KB",
                StaticLogFileName = true,
                File = fileName
            };

            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            hierarchy.Root.Level = Level.Debug;
            hierarchy.Configured = true;
        }

        public PatternLayout DefaultLayout
        {
            get { return _layout; }
        }

        public void AddConsoleAppender()
        {
            ConsoleAppender consoleAppender = new ConsoleAppender
            {
                Layout = _layout,
                Name = "ConsoleAppender"
            };

            consoleAppender.ActivateOptions();

            AddAppender(consoleAppender);
        }

        public void AddAppender(IAppender appender)
        {
            Hierarchy hierarchy =
                (Hierarchy)LogManager.GetRepository();

            hierarchy.Root.AddAppender(appender);
        }

        public ILog Log
        {
            get 
            {
                return LogManager.GetLogger("WmcTvOrganizer");
            }
        }
        
    }
}

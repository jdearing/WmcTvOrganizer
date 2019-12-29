using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WmcTvOrganizer.Core.Models;

namespace WmcTvOrganizer.Core.Test
{
    public class MockSettings : ISettings
    {
        public MockSettings()
        {
            TvSeries = new List<TvSeries>();
            IgnoreItems = new List<string>();
            WorkingDirectory = Path.Combine(".", "Working");
        }

        public async Task Load()
        {
            await Task.CompletedTask;
        }

        public async Task Save()
        {
            await Task.CompletedTask;
        }

        public IList<TvSeries> TvSeries { get; } 
        public IList<string> IgnoreItems { get; }
        public string WorkingDirectory { get; }
    }
}

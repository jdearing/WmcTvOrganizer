﻿using System;
using System.IO;
using Newtonsoft.Json;

namespace WmcTvOrganizer.Core.Models
{
    public class WmcItem
    {
        public TvSeries Series { get; set; }

        public string Title { get; set; }
        
        [JsonIgnore]
        public FileInfo File { get; set; }
        
        public DateTime BroadcastDate { get; set; }
        
        public string Description { get; set; }
        
        public TvDbEpisode TvDbEpisode { get; set; }
        
        public bool IsReRun { get; set; }
        
        public ItemType Type { get; set; }
        
        public int ReleaseYear { get; set; }

        public bool Protected { get; set; }
    }

    public enum ItemType { Unknown = 0, Tv, Movie };
}

using System;

namespace WmcTvOrganizer.Core.Models
{
    public class TvDbEpisode
    {
        public string Name { get; set; }
     
        public int EpisodeNumber { get; set; }
        
        public int SeasonNumber { get; set; }
        
        public DateTime FirstAired { get; set; }
    }
}

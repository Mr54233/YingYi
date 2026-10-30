using System;
using System.Collections.Generic;

namespace StrmAssistant.Web.Dto
{
    /// <summary>One episode advertised by a remote metadata provider but absent locally.</summary>
    public sealed class MissingEpisode
    {
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string Name { get; set; }
        public string Overview { get; set; }
        public DateTimeOffset? PremiereDate { get; set; }
        public string Source { get; set; }
    }

    public sealed class MissingEpisodesResponse
    {
        public string SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public bool Enabled { get; set; }
        public string Source { get; set; } = "none";
        public bool IsReadOnly { get; set; } = true;
        public bool IsBestEffort { get; set; } = true;
        public List<MissingEpisode> Episodes { get; set; } = new List<MissingEpisode>();
    }
}

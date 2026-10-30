using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using StrmAssistant.Web.Dto;

namespace StrmAssistant.Web.Api
{
    /// <summary>
    /// Read-only P3-4 endpoint. It never creates or updates library items.
    /// </summary>
    [Route("/Shows/{SeriesId}/MissingEpisodes", "GET")]
    [Authenticated]
    public class GetMissingEpisodes : IReturn<MissingEpisodesResponse>
    {
        [ApiMember(Name = "SeriesId", Description = "Series item id", IsRequired = true,
            DataType = "string", ParameterType = "path")]
        public string SeriesId { get; set; }

        [ApiMember(Name = "SeasonNumber", Description = "Season number", IsRequired = true,
            DataType = "int", ParameterType = "query")]
        public int SeasonNumber { get; set; }
    }
}

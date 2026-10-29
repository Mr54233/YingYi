using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using StrmAssistant.Rating;

namespace StrmAssistant.Web.Api
{
    [Route("/Items/{Id}/RatingsSummary", "GET")]
    [Authenticated]
    public class GetRatingsSummary : IReturn<RatingsSummary>
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string",
            ParameterType = "path")]
        public string Id { get; set; }
    }
}

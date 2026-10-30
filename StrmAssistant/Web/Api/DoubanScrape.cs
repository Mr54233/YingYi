using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    /// <summary>
    ///     POST /Items/{Id}/DoubanScrape — server-side handler behind the (batch 4)
    ///     DoubanScrapeButton. Applies Douban metadata (rating / overview / cast, per the
    ///     configured scope) to a single item and returns 204 on success, 400 when the
    ///     item cannot be scraped.
    /// </summary>
    [Route("/Items/{Id}/DoubanScrape", "POST")]
    [Authenticated]
    public class DoubanScrape : IReturnVoid, IReturn
    {
        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string",
            ParameterType = "path")]
        public string Id { get; set; }
    }
}

using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/indicator.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetIndicatorJs
    {
        public string Web { get; set; }
    }
}

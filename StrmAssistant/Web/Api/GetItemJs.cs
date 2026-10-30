using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/item.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetItemJs
    {
        public string Web { get; set; }
    }
}

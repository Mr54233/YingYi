using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/list.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetListJs
    {
        public string Web { get; set; }
    }
}

using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/listcontroller.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetListControllerJs
    {
        public string Web { get; set; }
    }
}

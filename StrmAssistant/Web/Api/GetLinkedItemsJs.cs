using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/linkeditems.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetLinkedItemsJs
    {
        public string Web { get; set; }
    }
}

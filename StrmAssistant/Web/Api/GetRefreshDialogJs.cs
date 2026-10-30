using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/refreshdialog.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetRefreshDialogJs
    {
        public string Web { get; set; }
    }
}

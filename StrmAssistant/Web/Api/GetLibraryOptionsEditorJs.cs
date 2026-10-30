using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/libraryoptionseditor.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetLibraryOptionsEditorJs
    {
        public string Web { get; set; }
    }
}

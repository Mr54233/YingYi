using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using StrmAssistant.Web.Api;
using StrmAssistant.Web.Helper;
using System;
using System.IO;

namespace StrmAssistant.Web.Service
{
    [Unauthenticated]
    public class ScriptInjectService : IService, IRequiresRequest
    {
        private readonly IHttpResultFactory _resultFactory;

        public ScriptInjectService(IHttpResultFactory resultFactory)
        {
            _resultFactory = resultFactory;
        }

        public IRequest Request { get; set; }

        public object Get(GetIndicatorJs request) => Serve("indicator.js", "BeautifyMissingMetadata", "application/x-javascript");
        public object Get(GetItemJs request) => Serve("item.js", "MarkPreviousEpisodesPlayed", "application/x-javascript");
        public object Get(GetListJs request) => Serve("list.js", "OptimizeMediaItemOrder", "application/x-javascript");
        public object Get(GetListControllerJs request) => Serve("listcontroller.js", "EnforceLibraryOrder", "application/x-javascript");
        public object Get(GetLinkedItemsJs request) => Serve("linkeditems.js", "ShowCollectionsFromSeasons", "application/x-javascript");
        public object Get(GetMediaInfoJs request) => Serve("mediainfo.js", "LibraryScopePlayProgress", "application/x-javascript");
        public object Get(GetRefreshDialogJs request) => Serve("refreshdialog.js", "EnhanceMissingEpisodes", "application/x-javascript");
        public object Get(GetLibraryOptionsEditorJs request) => Serve("libraryoptionseditor.js", "EnforceUserDisplayOption", "application/x-javascript");
        public object Get(GetFilterMenuHtml request) => Serve("filtermenu.html", "ReverseLogLines", "text/html; charset=utf-8");

        private object Serve(string resourceName, string optionKey, string contentType)
        {
            if (!ScriptInjectHelper.IsEnabled(optionKey))
            {
                return ScriptInjectHelper.DisabledResult(_resultFactory, Request);
            }

            using (var stream = ScriptInjectHelper.GetResourceStream(resourceName))
            {
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return _resultFactory.GetResult(Request, (ReadOnlyMemory<byte>)buffer.ToArray(), contentType, null);
                }
            }
        }
    }
}

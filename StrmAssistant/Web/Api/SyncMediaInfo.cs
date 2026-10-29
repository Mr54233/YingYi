using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/Items/SyncMediaInfo", "POST")]
    [Authenticated]
    public class SyncMediaInfo : IReturnVoid, IReturn
    {
        [ApiMember(Name = "LibraryId", Description = "Library Id (optional; blank = plugin MediaInfoExtract scope)",
            IsRequired = false, DataType = "string", ParameterType = "query")]
        public string LibraryId { get; set; }
    }
}

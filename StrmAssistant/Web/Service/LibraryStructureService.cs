using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Entities;
using StrmAssistant.Common;
using StrmAssistant.Web.Api;
using System;

namespace StrmAssistant.Web.Service
{
    public class LibraryStructureService : IService, IRequiresRequest
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        public LibraryStructureService(ILibraryManager libraryManager)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
        }

        public IRequest Request { get; set; }

        public void Post(CopyVirtualFolder request)
        {
            BaseItem sourceLibrary;

            try
            {
                sourceLibrary = _libraryManager.GetItemById(request.Id);
            }
            catch (Exception e)
            {
                // GetItemById throws (e.g. "Unrecognized Guid format") for a malformed Id.
                // Surface it as a client error instead of letting it become a 500.
                throw new ArgumentException("Invalid library id: " + request.Id, nameof(request.Id), e);
            }

            if (sourceLibrary == null)
            {
                throw new ArgumentException("Library not found: " + request.Id, nameof(request.Id));
            }

            var sourceOptions = _libraryManager.GetLibraryOptions(sourceLibrary);

            var targetOptions = LibraryApi.CopyLibraryOptions(sourceOptions);
            targetOptions.PathInfos = Array.Empty<MediaPathInfo>();

            var suffix = new Random().Next(100, 999).ToString();
            _libraryManager.AddVirtualFolder(sourceLibrary.Name + " #" + suffix, targetOptions, false);
        }
    }
}

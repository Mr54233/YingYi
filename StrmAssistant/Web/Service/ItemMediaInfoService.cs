using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Web.Service
{
    /// <summary>
    ///     Pro media-info APIs: ClearMediaInfo (per item) and SyncMediaInfo (queue a
    ///     media-info extraction pass).
    /// </summary>
    public class ItemMediaInfoService : BaseApiService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;

        public ItemMediaInfoService(ILibraryManager libraryManager, IItemRepository itemRepository)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
        }

        public void Any(ClearMediaInfo request)
        {
            var item = _libraryManager.GetItemById(request.Id);

            if (!(item is Video))
            {
                return;
            }

            var user = GetUserForRequest(null);
            var collectionFolders = _libraryManager.GetCollectionFolders(item);

            if (user is null)
            {
                if (!item.CanDelete())
                {
                    return;
                }
            }
            else if (!item.CanDelete(user, collectionFolders))
            {
                return;
            }

            // NOTE: BaseItem.MediaStreams is not hydrated from mediastreams2 by GetItemById
            // (it is populated from MediaSources during probe). Query the repository instead.
            // GetMediaStreams signature varies: 4.8 = (MediaStreamQuery), 4.9/4.10 = (MediaStreamQuery, CancellationToken)
            // — the compile-time reference is 4.8, so a direct call throws MissingMethodException on 4.9/4.10.
            var existing = QueryMediaStreams(item.InternalId);
            if (existing == null || existing.Count == 0)
            {
                return;
            }

            item.MediaStreams = new List<MediaStream>();
            _itemRepository.SaveMediaStreams(item.InternalId, item.MediaStreams, CancellationToken.None);

            _logger.Info("ClearMediaInfo - Cleared media info for: " + item.Name);
        }

        /// <summary>
        ///     Reads mediastreams2 rows for an item across Emby 4.8/4.9/4.10, whose
        ///     IItemRepository.GetMediaStreams signatures differ by a CancellationToken parameter.
        /// </summary>
        private List<MediaStream> QueryMediaStreams(long internalId)
        {
            try
            {
                var method = _itemRepository.GetType().GetMethods()
                    .FirstOrDefault(m =>
                        m.Name == "GetMediaStreams" &&
                        m.GetParameters().Length >= 1 &&
                        m.GetParameters()[0].ParameterType == typeof(MediaStreamQuery));
                if (method == null)
                {
                    _logger.Warn("ClearMediaInfo - GetMediaStreams not found on IItemRepository");
                    return null;
                }

                var args = new List<object> { new MediaStreamQuery { ItemId = internalId } };
                var parameters = method.GetParameters();
                if (parameters.Length == 2)
                {
                    args.Add(CancellationToken.None);
                }

                return (List<MediaStream>)method.Invoke(_itemRepository, args.ToArray());
            }
            catch (Exception e)
            {
                _logger.Warn("ClearMediaInfo - GetMediaStreams reflection failed: " + e.Message);
                return null;
            }
        }

        public void Any(SyncMediaInfo request)
        {
            List<BaseItem> items;
            if (string.IsNullOrWhiteSpace(request.LibraryId))
            {
                items = Plugin.LibraryApi.FetchPreExtractTaskItems();
            }
            else
            {
                var library = _libraryManager.GetItemById(request.LibraryId);
                if (library == null)
                {
                    return;
                }

                items = new List<BaseItem>(_libraryManager.GetItemList(new InternalItemsQuery
                {
                    Parent = library,
                    Recursive = true,
                    MediaTypes = new[] { MediaType.Video },
                    HasAudioStream = false
                }));
            }

            if (items.Count == 0)
            {
                _logger.Info("SyncMediaInfo - No items to sync");
                return;
            }

            _logger.Info("SyncMediaInfo - Queued media info sync for {0} item(s)", items.Count);

            // Detached background pass: the API returns immediately (accepted), the
            // extraction itself runs through the same orchestration the scheduled
            // task uses.
            Task.Run(() =>
            {
                var done = 0;
                foreach (var item in items)
                {
                    try
                    {
                        Plugin.LibraryApi.OrchestrateMediaInfoProcessAsync(item, "SyncMediaInfo API",
                            CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        _logger.Error("SyncMediaInfo - Item failed: " + item.Name);
                        _logger.Error(e.Message);
                    }

                    Interlocked.Increment(ref done);
                    if (done % 50 == 0)
                    {
                        _logger.Info("SyncMediaInfo - Progress {0}/{1}", done, items.Count);
                    }
                }

                _logger.Info("SyncMediaInfo - Completed {0} item(s)", done);
            });
        }
    }
}

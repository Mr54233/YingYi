using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Douban;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    ///     Scheduled task that pre-populates the local Douban cache: for every Movie and
    ///     Series that already carries a Douban provider id, fetches the detail payload
    ///     once, writes the cache JSON, and back-fills the item's rating when the library
    ///     has no community rating yet (also refreshes CriticRating from the same payload).
    ///     Skips blacklisted subjects (in_blacklist == true). The whole flow is network
    ///     bound, so all waits are async and per-item failures never abort the task.
    /// </summary>
    public class BuildDoubanCacheTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly DoubanClient _doubanClient;
        private readonly DoubanCacheService _doubanCacheService;

        public BuildDoubanCacheTask(ILibraryManager libraryManager,
            MediaBrowser.Common.Net.IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _doubanClient = new DoubanClient(_logger, httpClient, jsonSerializer);
            _doubanCacheService = new DoubanCacheService(_logger, jsonSerializer);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var options = Plugin.Instance.GetPluginOptions().MetadataEnhanceOptions;
            if (options == null || !options.EnableDoubanAssistScraping)
            {
                _logger.Info("Douban - Build Cache Task skipped: EnableDoubanAssistScraping is OFF");
                return;
            }

            _logger.Info("Douban - Build Cache Task Execute");
            await Task.Yield();
            progress.Report(0);

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { nameof(Movie), nameof(Series) },
                Recursive = true
            });
            _logger.Info("Douban - Build Cache Task candidates: " + items.Length);

            int fetched = 0, cached = 0, blacklisted = 0, rated = 0, failed = 0;
            double total = Math.Max(1, items.Length);

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("Douban - Build Cache Task Cancelled at {0}/{1}", fetched, total);
                    break;
                }

                progress.Report(fetched / total * 100);

                string doubanId = null;
                try
                {
                    if (item.ProviderIds == null ||
                        !item.ProviderIds.TryGetValue("Douban", out doubanId) ||
                        string.IsNullOrWhiteSpace(doubanId))
                    {
                        fetched++;
                        continue;
                    }
                }
                catch (Exception e)
                {
                    _logger.Debug("Douban - Failed reading provider ids of '{0}': {1}", item.Name, e.Message);
                    fetched++;
                    failed++;
                    continue;
                }

                try
                {
                    var detail = item is Series
                        ? await _doubanClient.GetTvDetailAsync(doubanId, cancellationToken).ConfigureAwait(false)
                        : await _doubanClient.GetMovieDetailAsync(doubanId, cancellationToken)
                            .ConfigureAwait(false);

                    if (detail == null)
                    {
                        failed++;
                    }
                    else if (detail.in_blacklist == true)
                    {
                        blacklisted++;
                    }
                    else
                    {
                        _doubanCacheService.SaveDetail(detail, doubanId);
                        cached++;

                        if (ApplyRatingIfMissing(item, detail))
                        {
                            rated++;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _logger.Debug("Douban - Build Cache Task failed on '{0}': {1}", item.Name, e.Message);
                    failed++;
                }

                fetched++;
            }

            progress.Report(100.0);
            _logger.Info(
                "Douban - Build Cache Task Complete. scanned: {0}, withDoubanId: {1}, cached: {2}, blacklisted: {3}, rated: {4}, failed: {5}",
                items.Length, fetched, cached, blacklisted, rated, failed);
        }

        /// <summary>
        ///     Back-fills CommunityRating (douban score, 0-10) and CriticRating only when
        ///     the item currently has no/zero rating, so we never overwrite curator data.
        ///     Returns true when the item's rating was written.
        /// </summary>
        private bool ApplyRatingIfMissing(BaseItem item, Douban.Dto.DoubanDetailResponse detail)
        {
            try
            {
                var value = detail.rating?.value;
                if (!value.HasValue || value.Value <= 0)
                {
                    return false;
                }

                var changed = false;
                if (!item.CommunityRating.HasValue || item.CommunityRating.Value == 0f)
                {
                    item.CommunityRating = (float)value.Value;
                    changed = true;
                }

                if (!item.CriticRating.HasValue || item.CriticRating.Value == 0f)
                {
                    item.CriticRating = (float)value.Value;
                    changed = true;
                }

                if (changed)
                {
                    // UpdateToRepository 只写库不刷新内存缓存（见 PopulateDoubanIdTask 同处注释），
                    // 走 UpdateItems + MetadataEdit 保证同进程内立即可见。
                    _libraryManager.UpdateItems(new List<BaseItem> { item }, null,
                        ItemUpdateType.MetadataEdit, true, false, null, CancellationToken.None);
                }

                return changed;
            }
            catch (Exception e)
            {
                _logger.Debug("Douban - Rating back-fill failed for '{0}': {1}", item.Name, e.Message);
                return false;
            }
        }

        public string Category => Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
            Plugin.Instance.DefaultUICulture);

        public string Key => "BuildDoubanCacheTask";

        public string Description => Resources.ResourceManager.GetString(
            "BuildDoubanCacheTask_Description_Builds_a_local_Douban_metadata_cache",
            Plugin.Instance.DefaultUICulture);

        public string Name => "Build Douban Cache";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}

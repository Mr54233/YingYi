using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Common.Net;
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
    ///     Scheduled task that fills in missing Douban provider ids: for every Movie /
    ///     Series / Season that has an IMDb id but no Douban id, resolves the Douban id
    ///     through the WikiData bridge (Rating.WikiDataClient) and writes it into
    ///     ProviderIds["Douban"]. Pure id plumbing; never fetches metadata itself.
    /// </summary>
    public class PopulateDoubanIdTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly DoubanIdResolver _resolver;

        public PopulateDoubanIdTask(ILibraryManager libraryManager, IHttpClient httpClient,
            IJsonSerializer jsonSerializer)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _resolver = new DoubanIdResolver(_logger, httpClient, jsonSerializer);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("Douban - Populate ID Task Execute");
            await Task.Yield();
            progress.Report(0);

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { nameof(Movie), nameof(Series), nameof(Season) },
                Recursive = true
            });
            _logger.Info("Douban - Populate ID Task candidates: " + items.Length);

            int updated = 0, skipped = 0, unresolved = 0;
            double total = Math.Max(1, items.Length);

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("Douban - Populate ID Task Cancelled");
                    break;
                }

                progress.Report((updated + skipped + unresolved) / total * 100);

                string existing = null, imdb = null;
                try
                {
                    if (item.ProviderIds == null)
                    {
                        skipped++;
                        continue;
                    }

                    if (item.ProviderIds.TryGetValue("Douban", out existing) &&
                        !string.IsNullOrWhiteSpace(existing))
                    {
                        skipped++;
                        continue;
                    }

                    if (!item.ProviderIds.TryGetValue("Imdb", out imdb) ||
                        string.IsNullOrWhiteSpace(imdb))
                    {
                        skipped++;
                        continue;
                    }
                }
                catch (Exception e)
                {
                    _logger.Debug("Douban - Failed reading provider ids of '{0}': {1}", item.Name, e.Message);
                    skipped++;
                    continue;
                }

                string doubanId = null;
                try
                {
                    doubanId = await _resolver.ResolveByItemAsync(item, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _logger.Debug("Douban - Resolve failed for '{0}': {1}", item.Name, e.Message);
                }

                if (string.IsNullOrWhiteSpace(doubanId))
                {
                    unresolved++;
                    continue;
                }

                try
                {
                    item.ProviderIds["Douban"] = doubanId;
                    // UpdateToRepository 只写库不刷新内存缓存：刚保存的 Douban id 在
                    // 同进程内再读会得到旧值（实测 4.10.2.0：API/任务都看不到新 id）。
                    // 走 UpdateItems + MetadataEdit 与 LibraryApi.UpdateDateModifiedLastSaved 同型，
                    // 既落库也同步缓存。
                    _libraryManager.UpdateItems(new List<BaseItem> { item }, null,
                        ItemUpdateType.MetadataEdit, true, false, null, CancellationToken.None);
                    updated++;
                    _logger.Info("Douban - Populate ID Task: '{0}' IMDb {1} -> Douban {2}",
                        item.Name, imdb, doubanId);
                }
                catch (Exception e)
                {
                    _logger.Debug("Douban - Failed saving Douban id for '{0}': {1}", item.Name, e.Message);
                    unresolved++;
                }
            }

            progress.Report(100.0);
            _logger.Info(
                "Douban - Populate ID Task Complete. updated: {0}, skipped: {1}, unresolved: {2}, total: {3}",
                updated, skipped, unresolved, items.Length);
        }

        public string Category => Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
            Plugin.Instance.DefaultUICulture);

        public string Key => "PopulateDoubanIdTask";

        public string Description => Resources.ResourceManager.GetString(
            "PopulateDoubanIdTask_Description_Resolve_Douban_IDs_from_IMDb_via_WikiData",
            Plugin.Instance.DefaultUICulture);

        public string Name => "Populate Douban IDs";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}

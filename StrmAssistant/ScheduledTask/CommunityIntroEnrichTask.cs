using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using StrmAssistant.IntroSkip;
using StrmAssistant.Options;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkipOptions = StrmAssistant.Options.IntroSkipOptions;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    ///     Fills missing intro markers from community databases (IntroDB, optionally TheIntroDB).
    ///     Runs only where the local fingerprint gave up (failure record present, no marker yet).
    /// </summary>
    public class CommunityIntroEnrichTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;
        private readonly CommunityIntroSource _communityIntroSource;

        public CommunityIntroEnrichTask(ILibraryManager libraryManager, IItemRepository itemRepository,
            IHttpClient httpClient, IJsonSerializer jsonSerializer, IFileSystem fileSystem)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _communityIntroSource = new CommunityIntroSource(Plugin.Instance.Logger, httpClient, jsonSerializer,
                fileSystem, itemRepository);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("CommunityIntro - Scheduled Task Execute");
            await Task.Yield();
            progress.Report(0);

            var options = Plugin.Instance.GetPluginOptions().IntroSkipOptions;

            if (!options.UnlockIntroSkip)
            {
                _logger.Info("CommunityIntro - Intro Skip Enhanced is locked, skip");
                progress.Report(100);
                return;
            }

            if (!options.EnableCommunityIntroSource)
            {
                _logger.Info("CommunityIntro - Community Intro Source is disabled, skip");
                progress.Report(100);
                return;
            }

            try
            {
                await ExecuteInternalAsync(options, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("CommunityIntro - Cancelled");
            }
            catch (Exception e)
            {
                _logger.Error("CommunityIntro - Task failed: {0}", e.Message);
                _logger.Debug(e.StackTrace);
            }
            finally
            {
                progress.Report(100);
            }
        }

        private async Task ExecuteInternalAsync(IntroSkipOptions options, CancellationToken cancellationToken,
            IProgress<double> progress)
        {
            var sources = options.CommunityIntroSources?
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList() ?? new List<string>();

            var enableIntroDb = sources.Contains("IntroDb", StringComparer.OrdinalIgnoreCase);
            var enableTheIntroDb = sources.Contains("TheIntroDb", StringComparer.OrdinalIgnoreCase);

            if (!enableIntroDb && !enableTheIntroDb)
            {
                _logger.Info("CommunityIntro - No community source selected");
                return;
            }

            var episodes = FetchEpisodes();
            var total = episodes.Count;
            var written = 0;
            var skipped = 0;
            var failed = 0;
            var index = 0;

            _logger.Info("CommunityIntro - {0} candidate episodes", total);

            foreach (var episode in episodes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("CommunityIntro - Cancelled at {0}/{1}", index, total);
                    break;
                }

                try
                {
                    // local fingerprint marker wins: community never touches it
                    if (Plugin.ChapterApi.HasIntro(episode))
                    {
                        skipped++;
                    }
                    // community only fills gaps where local fingerprint gave up
                    else if (string.IsNullOrEmpty(_itemRepository.GetIntroDetectionFailureResult(episode.InternalId)))
                    {
                        skipped++;
                    }
                    else if (_communityIntroSource.TryGetCachedVerdict(episode.InternalId,
                        options.CommunitySourceCacheDays, out _))
                    {
                        skipped++;
                    }
                    else
                    {
                        var result = await _communityIntroSource.LookupAsync(episode, enableIntroDb, enableTheIntroDb,
                            options.CommunitySourceTimeoutSeconds, options.CommunitySourceMinConfidence,
                            cancellationToken).ConfigureAwait(false);

                        var hasIntro = result != null && result.IntroStartTicks != null &&
                                       result.IntroEndTicks != null;
                        var hasCredits = result != null && result.CreditsStartTicks != null;

                        if (hasIntro || hasCredits)
                        {
                            if (options.CommunitySourceRequireManualConfirm)
                            {
                                // candidate only: neither write nor cache, so it is reported on every run
                                _logger.Info(
                                    "CommunityIntro - Manual confirm required, candidate intro {0}-{1}s credits {2}s for {3} ({4})",
                                    result.IntroStartTicks / TimeSpan.TicksPerSecond,
                                    result.IntroEndTicks / TimeSpan.TicksPerSecond,
                                    result.CreditsStartTicks / TimeSpan.TicksPerSecond,
                                    episode.Name, result.Source);
                                skipped++;
                            }
                            else
                            {
                                _communityIntroSource.WriteMarkers(episode, result);
                                _communityIntroSource.SaveCacheVerdict(episode.InternalId, result);
                                written++;
                            }
                        }
                        else
                        {
                            // confirmed-no-intro verdicts and no-data are both cached as negatives
                            _communityIntroSource.SaveCacheVerdict(episode.InternalId, result);
                            skipped++;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("CommunityIntro - Cancelled");
                    break;
                }
                catch (Exception e)
                {
                    failed++;
                    _logger.Debug("CommunityIntro - Episode {0} failed: {1}", episode.Name, e.Message);
                }

                index++;
                if (index % 10 == 0 || index == total)
                {
                    progress.Report((double)index / total * 100.0);
                }
            }

            _logger.Info("CommunityIntro - Finished: written {0}, skipped {1}, failed {2}", written, skipped, failed);
        }

        private List<Episode> FetchEpisodes()
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { nameof(Episode) },
                HasPath = true,
                MediaTypes = new[] { MediaType.Video },
                Recursive = true,
                GroupByPresentationUniqueKey = false
            };

            ApplyMarkerScope(query, Plugin.Instance.GetPluginOptions().IntroSkipOptions.MarkerEnabledLibraryScope);

            return _libraryManager.GetItemList(query).OfType<Episode>().ToList();
        }

        /// <summary>
        /// Restrict the query to the libraries enabled for marker detection, mirroring
        /// FingerprintApi.UpdateLibraryPathsInScope: "-1" is the favorites-only mode.
        /// </summary>
        private void ApplyMarkerScope(InternalItemsQuery query, string markerEnabledLibraryScope)
        {
            try
            {
                var scope = markerEnabledLibraryScope ?? string.Empty;
                var validLibraryIds = Utility.GetValidLibraryIds(scope);

                if (validLibraryIds.Any() && validLibraryIds.All(id => id == "-1"))
                {
                    query.ParentIds = Plugin.FingerprintApi.GetAllFavoriteSeasons().DefaultIfEmpty(-1).ToArray();
                    return;
                }

                var libraryPaths = _libraryManager.GetVirtualFolders()
                    .Where(f => f.LibraryOptions.EnableMarkerDetection &&
                                (f.CollectionType == CollectionType.TvShows.ToString() ||
                                 f.CollectionType is null) &&
                                (!validLibraryIds.Any() || validLibraryIds.Contains(f.Id)))
                    .SelectMany(f => f.Locations)
                    .Select(l => l.EndsWith(Path.DirectorySeparatorChar.ToString())
                        ? l
                        : l + Path.DirectorySeparatorChar)
                    .ToArray();

                if (libraryPaths.Any())
                {
                    query.PathStartsWithAny = libraryPaths;
                }
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Failed to apply marker library scope: {0}", e.Message);
            }
        }

        public string Name => Resources.ResourceManager.GetString(
            "CommunityIntroEnrichTask_Name_Community_Intro_Enrichment", Plugin.Instance.DefaultUICulture);

        public string Key => "CommunityIntroEnrichTask";

        public string Description => Resources.ResourceManager.GetString(
            "CommunityIntroEnrichTask_Description_Fills_missing_intro_markers_from_community_database",
            Plugin.Instance.DefaultUICulture);

        public string Category => Resources.ResourceManager.GetString(
            "PluginOptions_EditorTitle_Strm_Assistant", Plugin.Instance.DefaultUICulture);

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}

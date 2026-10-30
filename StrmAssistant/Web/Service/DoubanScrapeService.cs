using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Douban;
using StrmAssistant.Douban.Dto;
using StrmAssistant.Options;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Web.Service
{
    /// <summary>
    ///     Handles POST /Items/{Id}/DoubanScrape — the server side of the (batch 4)
    ///     DoubanScrapeButton. Fetches (cache-first) the Douban detail of an item that
    ///     already carries a Douban provider id, then applies the fields allowed by
    ///     MetadataEnhanceOptions.DoubanAssistScrapeScope:
    ///     Rating → CommunityRating/CriticRating (only when missing/0, same rule as the
    ///     cache task); NameOverview → Name + Overview; Cast → directors + actors via
    ///     ILibraryManager.UpdatePeople; Poster → cached payload only in this batch (the
    ///     actual image write needs IProviderManager plumbing; see delivery report).
    ///     A successful apply returns 204 (void); an unscrapable item throws, mapping to 400.
    /// </summary>
    public class DoubanScrapeService : BaseApiService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly DoubanClient _doubanClient;
        private readonly DoubanCacheService _doubanCacheService;

        public DoubanScrapeService(ILibraryManager libraryManager, IHttpClient httpClient,
            IJsonSerializer jsonSerializer)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _doubanClient = new DoubanClient(_logger, httpClient, jsonSerializer);
            _doubanCacheService = new DoubanCacheService(_logger, jsonSerializer);
        }

        public async Task Post(DoubanScrape request)
        {
            if (string.IsNullOrWhiteSpace(request?.Id))
            {
                throw new ArgumentException("Item id is required", "Id");
            }

            var item = _libraryManager.GetItemById(request.Id);
            if (item == null)
            {
                throw new ArgumentException("Item not found: " + request.Id, nameof(request.Id));
            }

            string doubanId = null;
            try
            {
                if (item.ProviderIds == null ||
                    !item.ProviderIds.TryGetValue("Douban", out doubanId) ||
                    string.IsNullOrWhiteSpace(doubanId))
                {
                    throw new ArgumentException(
                        "Item has no Douban id: " + item.Name + ". Run PopulateDoubanIdTask first.",
                        nameof(request.Id));
                }
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ArgumentException("Failed reading Douban id of " + item.Name + ": " + e.Message,
                    nameof(request.Id));
            }

            var detail = _doubanCacheService.TryLoadDetail(doubanId);
            if (detail == null)
            {
                detail = item is Series
                    ? await _doubanClient.GetTvDetailAsync(doubanId, CancellationToken.None).ConfigureAwait(false)
                    : await _doubanClient.GetMovieDetailAsync(doubanId, CancellationToken.None)
                        .ConfigureAwait(false);

                if (detail == null)
                {
                    throw new ArgumentException(
                        "Douban request failed for subject " + doubanId + " (network, throttle timeout or non-200)",
                        nameof(request.Id));
                }

                if (detail.in_blacklist == true)
                {
                    throw new ArgumentException(
                        "Douban subject " + doubanId + " is blacklisted", nameof(request.Id));
                }

                _doubanCacheService.SaveDetail(detail, doubanId);
            }

            var scope = Plugin.Instance.GetPluginOptions().MetadataEnhanceOptions?.DoubanAssistScrapeScope;
            var flags = ParseScope(scope);

            var changed = false;
            if (flags.Rating)
            {
                changed |= ApplyRating(item, detail);
            }

            if (flags.NameOverview)
            {
                changed |= ApplyNameOverview(item, detail);
            }

            if (flags.Cast)
            {
                changed |= ApplyCast(item, detail);
            }

            if (changed)
            {
                // UpdateToRepository 只写库不刷新内存缓存（见 PopulateDoubanIdTask 同处注释），
                // 走 UpdateItems + MetadataEdit 保证同进程内立即可见。
                _libraryManager.UpdateItems(new List<BaseItem> { item }, null,
                    ItemUpdateType.MetadataEdit, true, false, null, CancellationToken.None);
            }

            _logger.Info("Douban - Scrape applied to '{0}' (subject {1}): rating={2}, nameOverview={3}, cast={4}",
                item.Name, doubanId, flags.Rating, flags.NameOverview, flags.Cast);
        }

        private (bool Rating, bool Cast, bool NameOverview, bool Poster) ParseScope(string scope)
        {
            // Default (null/empty/malformed) is ALL scopes, matching the option default.
            if (string.IsNullOrWhiteSpace(scope))
            {
                return (true, true, true, true);
            }

            bool rating = false, cast = false, nameOverview = false, poster = false;
            foreach (var raw in scope.Split(','))
            {
                var part = raw.Trim();
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                if (Enum.TryParse<MetadataEnhanceOptions.DoubanAssistScrapeOption>(part, true, out var option))
                {
                    switch (option)
                    {
                        case MetadataEnhanceOptions.DoubanAssistScrapeOption.Rating:
                            rating = true;
                            break;
                        case MetadataEnhanceOptions.DoubanAssistScrapeOption.Cast:
                            cast = true;
                            break;
                        case MetadataEnhanceOptions.DoubanAssistScrapeOption.NameOverview:
                            nameOverview = true;
                            break;
                        case MetadataEnhanceOptions.DoubanAssistScrapeOption.Poster:
                            poster = true;
                            break;
                    }
                }
            }

            return (rating, cast, nameOverview, poster);
        }

        /// <summary>Same conservative rating rule as BuildDoubanCacheTask: only fill blanks.</summary>
        private bool ApplyRating(BaseItem item, DoubanDetailResponse detail)
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

                return changed;
            }
            catch (Exception e)
            {
                _logger.Debug("Douban - ApplyRating failed for '{0}': {1}", item.Name, e.Message);
                return false;
            }
        }

        /// <summary>
        ///     Douban's title/intro are the zh-CN source of truth, so unlike the rating we
        ///     overwrite existing values (that is the point of the manual button). Empty
        ///     payload fields are skipped to avoid blanking the item.
        /// </summary>
        private bool ApplyNameOverview(BaseItem item, DoubanDetailResponse detail)
        {
            try
            {
                var changed = false;

                if (!string.IsNullOrWhiteSpace(detail.title))
                {
                    item.Name = detail.title;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(detail.intro))
                {
                    item.Overview = detail.intro;
                    changed = true;
                }

                return changed;
            }
            catch (Exception e)
            {
                _logger.Debug("Douban - ApplyNameOverview failed for '{0}': {1}", item.Name, e.Message);
                return false;
            }
        }

        /// <summary>
        ///     Replaces the item's people with Douban directors + actors (up to a sane
        ///     cap so a 100-strong cast cannot flood the library). Uses the same 2-arg
        ///     UpdatePeople call as Common/MetadataApi.cs.
        /// </summary>
        private bool ApplyCast(BaseItem item, DoubanDetailResponse detail)
        {
            const int maxActors = 30;

            try
            {
                var people = new List<PersonInfo>();
                if (detail.directors != null)
                {
                    foreach (var director in detail.directors)
                    {
                        if (director == null || string.IsNullOrWhiteSpace(director.name))
                        {
                            continue;
                        }

                        people.Add(new PersonInfo { Name = director.name, Type = PersonType.Director });
                    }
                }

                if (detail.actors != null)
                {
                    foreach (var actor in detail.actors)
                    {
                        if (actor == null || string.IsNullOrWhiteSpace(actor.name))
                        {
                            continue;
                        }

                        if (people.Count >= maxActors)
                        {
                            break;
                        }

                        people.Add(new PersonInfo { Name = actor.name, Type = PersonType.Actor });
                    }
                }

                if (people.Count == 0)
                {
                    return false;
                }

                _libraryManager.UpdatePeople(item, people);
                return true;
            }
            catch (Exception e)
            {
                _logger.Debug("Douban - ApplyCast failed for '{0}': {1}", item.Name, e.Message);
                return false;
            }
        }
    }
}

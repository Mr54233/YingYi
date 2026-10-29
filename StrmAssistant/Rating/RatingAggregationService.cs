using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Rating.Dto;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Rating
{
    public class RatingAggregationService
    {
        private readonly OmdbClient _omdb;
        private readonly MdbListClient _mdb;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Builds the full aggregation chain (OMDb + MDBList clients) from Emby-registered
        /// services, so no extra DI registration is needed at the call site.
        /// </summary>
        public RatingAggregationService(ILibraryManager libraryManager, IHttpClient httpClient,
            IJsonSerializer jsonSerializer)
            : this(new OmdbClient(Plugin.Instance.Logger, httpClient, jsonSerializer),
                new MdbListClient(Plugin.Instance.Logger, httpClient, jsonSerializer), libraryManager)
        {
        }

        public RatingAggregationService(OmdbClient omdb, MdbListClient mdb, ILibraryManager libraryManager)
        {
            _omdb = omdb;
            _mdb = mdb;
            _libraryManager = libraryManager;
        }

        public async Task<float?> GetRottenTomatoAsync(BaseItem item, CancellationToken ct)
        {
            if (item == null)
            {
                return null;
            }

            var imdbId = GetImdbId(item);
            if (string.IsNullOrWhiteSpace(imdbId))
            {
                return null;
            }

            OmdbResponse omdb = null;
            try
            {
                omdb = await _omdb.GetByImdbAsync(imdbId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                StrmAssistant.Plugin.Instance?.Logger?.Warn("Rating - OMDb lookup failed for {0}: {1}", item.Name, e.Message);
            }

            var score = omdb?.GetRottenTomatoScore();
            if (!score.HasValue || score.Value == 0)
            {
                MdbListResponse mdb = null;
                try
                {
                    mdb = await _mdb.GetByImdbAsync(imdbId, IsSeriesLike(item), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    StrmAssistant.Plugin.Instance?.Logger?.Warn("Rating - MDBList lookup failed for {0}: {1}", item.Name, e.Message);
                }

                score = mdb?.GetRottenTomatoScore();
            }

            return !score.HasValue || score.Value == 0 ? (float?)null : score;
        }

        public async Task<RatingsSummary> BuildSummaryAsync(BaseItem item, CancellationToken ct)
        {
            if (item == null)
            {
                return new RatingsSummary();
            }

            var result = new RatingsSummary
            {
                Douban = item.CommunityRating.HasValue ? (double?)item.CommunityRating.Value : null
            };

            var imdbId = GetImdbId(item);
            if (!string.IsNullOrWhiteSpace(imdbId))
            {
                var hasMdbKey = !string.IsNullOrWhiteSpace(
                    StrmAssistant.Plugin.Instance?.GetPluginOptions()?.MetadataRefreshOptions?.MdbListApiKey);
                if (!hasMdbKey)
                {
                    _mdb.WarnMissingApiKey();
                }
                else
                {
                    try
                    {
                        var mdb = await _mdb.GetByImdbAsync(imdbId, IsSeriesLike(item), ct).ConfigureAwait(false);
                        if (mdb != null)
                        {
                            var rt = mdb.GetRottenTomatoScore();
                            var imdb = mdb.GetImdbScore();
                            result.Tomatometer = rt.HasValue && rt.Value != 0 ? (double?)rt.Value : null;
                            result.IMDb = imdb.HasValue ? (double?)imdb.Value : null;
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        StrmAssistant.Plugin.Instance?.Logger?.Warn("Rating - MDBList summary lookup failed for {0}: {1}", item.Name, e.Message);
                    }
                }
            }

            // When the remote sources are unavailable, expose the item's existing critic score as its cached Tomatometer.
            if (!result.Tomatometer.HasValue && item.CriticRating.HasValue && item.CriticRating.Value != 0)
            {
                result.Tomatometer = item.CriticRating.Value;
            }

            return result;
        }

        public void ApplyCriticRating(BaseItem item, float? rt)
        {
            if (item == null)
            {
                return;
            }

            item.CriticRating = rt;
            _libraryManager.UpdateItems(new List<BaseItem> { item }, null, ItemUpdateType.None, true, false, null,
                CancellationToken.None);
        }

        private static string GetImdbId(BaseItem item)
        {
            try
            {
                if (item.ProviderIds != null && item.ProviderIds.TryGetValue("Imdb", out var imdb) &&
                    !string.IsNullOrWhiteSpace(imdb))
                {
                    return imdb;
                }
            }
            catch (Exception e)
            {
                StrmAssistant.Plugin.Instance?.Logger?.Warn("Rating - Failed to read IMDb provider id for {0}: {1}", item?.Name, e.Message);
            }

            return null;
        }

        private static bool IsSeriesLike(BaseItem item)
        {
            return item is MediaBrowser.Controller.Entities.TV.Series ||
                   item is MediaBrowser.Controller.Entities.TV.Season ||
                   item is MediaBrowser.Controller.Entities.TV.Episode;
        }
    }
}

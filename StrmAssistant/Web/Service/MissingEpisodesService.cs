using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using StrmAssistant.Web.Api;
using StrmAssistant.Web.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace StrmAssistant.Web.Service
{
    /// <summary>
    ///     P3-4 low-risk read-only MVP. Uses the public <see cref="IProviderManager.GetAllEpisodes"/>
    ///     surface (present in the 4.8 compile-time reference and still identical in 4.10) to ask the
    ///     configured metadata providers which episodes a series should have, then diffs that against
    ///     the local library. The endpoint never writes, creates placeholders, or refreshes items,
    ///     and any failure degrades to an empty result instead of an error.
    /// </summary>
    public sealed class MissingEpisodesService : BaseApiService
    {
        private const int RemoteQueryTimeoutMs = 15000;

        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;

        /// <summary>
        ///     Optional host/test adapter: (series id, season number) -> expected episode numbers.
        ///     When set it replaces the remote provider call, which keeps unit tests offline.
        /// </summary>
        public static Func<string, int, IEnumerable<int>> ExpectedEpisodeResolver { get; set; }

        public MissingEpisodesService(ILibraryManager libraryManager, IProviderManager providerManager)
        {
            _libraryManager = libraryManager;
            _providerManager = providerManager;
        }

        public MissingEpisodesResponse Get(GetMissingEpisodes request)
        {
            var response = new MissingEpisodesResponse
            {
                SeriesId = request?.SeriesId,
                SeasonNumber = request?.SeasonNumber ?? 0,
                Enabled = Plugin.Instance?.GetPluginOptions()?.UIFunctionOptions?.EnhanceMissingEpisodes == true
            };

            if (!response.Enabled)
            {
                // Same gate style as the script-injection endpoints: a hard 404, set on the
                // raw response because Emby maps thrown exceptions to a generic 500.
                Request.Response.StatusCode = (int)HttpStatusCode.NotFound;
                Request.Response.StatusDescription = "Not Found";
                Request.Response.ContentType = "text/plain";
                return new MissingEpisodesResponse();
            }

            if (string.IsNullOrWhiteSpace(response.SeriesId) || response.SeasonNumber < 0)
            {
                return response;
            }

            try
            {
                var series = _libraryManager.GetItemById(response.SeriesId) as Series;
                if (series == null) return response;

                var localEpisodes = series.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { nameof(Episode) },
                        Recursive = true,
                        HasIndexNumber = true
                    })
                    .OfType<Episode>()
                    .Where(e => e.ParentIndexNumber == response.SeasonNumber && e.IndexNumber.HasValue)
                    .Select(e => e.IndexNumber.Value);

                string source = null;
                var expected = ExpectedEpisodeResolver != null
                    ? ExpectedEpisodeResolver(response.SeriesId, response.SeasonNumber)
                    : FetchRemoteEpisodes(series, response.SeasonNumber, out source);

                response.Source = source ?? "none";
                response.Episodes = MissingEpisodesCalculator
                    .ComputeMissing(response.SeasonNumber, expected, localEpisodes);
            }
            catch (Exception ex)
            {
                // This endpoint is advisory only: an unavailable host surface must never
                // affect playback or make the refresh dialog fail.
                Plugin.Instance?.Logger?.Debug("MissingEpisodes - returning empty result: " + ex.Message);
                response.Source = "none";
                response.Episodes = new List<MissingEpisode>();
            }

            return response;
        }

        /// <summary>
        ///     Calls the public provider pipeline for the full remote episode list of the series and
        ///     keeps only the entries of the requested season. A null/empty remote answer simply
        ///     yields no expected numbers, which the diff turns into an empty result.
        /// </summary>
        private IEnumerable<int> FetchRemoteEpisodes(Series series, int seasonNumber, out string source)
        {
            source = null;

            if (_providerManager == null) return Enumerable.Empty<int>();

            // The host pipeline dereferences libraryOptions (IsMetadataFetcherEnabled),
            // so null is not acceptable; GetLibraryOptions mirrors Emby's own GetCredits flow.
            var libraryOptions = _libraryManager.GetLibraryOptions(series);

            using (var timeout = new CancellationTokenSource(RemoteQueryTimeoutMs))
            {
                var remote = _providerManager.GetAllEpisodes(series, libraryOptions, timeout.Token);
                remote.Wait(timeout.Token);
                var results = remote.Result;

                if (results == null || results.Length == 0) return Enumerable.Empty<int>();

                source = results.Select(r => r.SearchProviderName).FirstOrDefault(n => !string.IsNullOrEmpty(n));
                return results
                    .Where(r => r.ParentIndexNumber == seasonNumber && r.IndexNumber > 0)
                    .Select(r => r.IndexNumber.Value);
            }
        }
    }
}

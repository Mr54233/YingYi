using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant;
using StrmAssistant.Rating.Dto;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Rating
{
    public class MdbListClient : RatingHttpClientBase
    {
        // MDBList's IMDb route is /imdb/{movie|show}/{imdbId}. Omitting the media type segment
        // returns 405 Method Not Allowed (verified live against api.mdblist.com on 2026-10-29),
        // which would silently look like an auth problem in the logs.
        private const string BaseUrl = "https://api.mdblist.com/imdb/";
        private static readonly ConcurrentDictionary<string, byte> WarnedMissingKeys =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        public MdbListClient(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer)
            : base(logger, httpClient, jsonSerializer)
        {
        }

        public void WarnMissingApiKey()
        {
            if (WarnedMissingKeys.TryAdd("MDBList", 0))
            {
                Logger?.Warn("Rating - MDBList API key is empty; rating lookups from this source are skipped.");
            }
        }

        public Task<MdbListResponse> GetByImdbAsync(string imdbId, CancellationToken cancellationToken)
        {
            return GetByImdbAsync(imdbId, false, cancellationToken);
        }

        /// <param name="isSeries">
        /// Selects the media type segment of the MDBList IMDb route: shows and movies live under
        /// different paths, and asking the wrong one yields a 404 that is indistinguishable from
        /// "this title has no ratings".
        /// </param>
        public Task<MdbListResponse> GetByImdbAsync(string imdbId, bool isSeries, CancellationToken cancellationToken)
        {
            var key = Plugin.Instance?.GetPluginOptions()?.MetadataRefreshOptions?.MdbListApiKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                WarnMissingApiKey();
                return Task.FromResult<MdbListResponse>(null);
            }

            if (string.IsNullOrWhiteSpace(imdbId))
            {
                return Task.FromResult<MdbListResponse>(null);
            }

            var mediaType = isSeries ? "show" : "movie";
            var url = BaseUrl + mediaType + "/" + Uri.EscapeDataString(imdbId) +
                      "?apikey=" + Uri.EscapeDataString(key.Trim());
            return GetJsonAsync<MdbListResponse>(url, cancellationToken);
        }
    }
}

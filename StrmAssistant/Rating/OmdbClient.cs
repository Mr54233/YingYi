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
    public class OmdbClient : RatingHttpClientBase
    {
        private const string BaseUrl = "https://www.omdbapi.com/";
        private static readonly ConcurrentDictionary<string, byte> WarnedMissingKeys =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        public OmdbClient(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer)
            : base(logger, httpClient, jsonSerializer)
        {
        }

        public Task<OmdbResponse> GetByImdbAsync(string imdbId, CancellationToken cancellationToken)
        {
            var key = Plugin.Instance?.GetPluginOptions()?.MetadataRefreshOptions?.OmdbApiKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                WarnMissingKeyOnce("OMDb", WarnedMissingKeys);
                return Task.FromResult<OmdbResponse>(null);
            }

            if (string.IsNullOrWhiteSpace(imdbId))
            {
                return Task.FromResult<OmdbResponse>(null);
            }

            return GetJsonAsync<OmdbResponse>(BaseUrl + "?i=" + Uri.EscapeDataString(imdbId) +
                "&apikey=" + Uri.EscapeDataString(key.Trim()), cancellationToken);
        }

        private void WarnMissingKeyOnce(string provider, ConcurrentDictionary<string, byte> warnedKeys)
        {
            if (warnedKeys.TryAdd(provider, 0))
            {
                Logger?.Warn("Rating - {0} API key is empty; rating lookups from this source are skipped.", provider);
            }
        }
    }
}

using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Douban.Dto;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Douban
{
    /// <summary>
    ///     The single gateway for all Douban network calls in this plugin.
    ///     Uses the m.douban.com rexxar v2 endpoints (calibrated 2026-10-29):
    ///     a mobile UA plus a per-subject Referer header gets full JSON without any
    ///     apiKey/signature/cookie. The frodo.douban.com endpoints 400 even with a
    ///     Referer and must NOT be used. Missing the Referer header itself yields 400.
    ///     All requests are rate limited (1 per 1100ms), time out after 15s via a
    ///     linked CTS, and every non-cancellation failure is swallowed into a null
    ///     return. Everything is async end to end; no sync-over-async.
    /// </summary>
    public class DoubanClient
    {
        private const string MovieDetailEndpointFormat = "https://m.douban.com/rexxar/api/v2/movie/{0}";
        private const string TvDetailEndpointFormat = "https://m.douban.com/rexxar/api/v2/tv/{0}";
        private const string SearchEndpointFormat =
            "https://m.douban.com/rexxar/api/v2/search/subjects?q={0}&type={1}";

        private const string MobileUserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

        private const int RequestIntervalMs = 1100;
        private const int TimeoutSeconds = 15;

        private static readonly object ThrottleLock = new object();
        private static long _lastRequestTicks;

        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;

        public DoubanClient(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
        }

        public Task<DoubanDetailResponse> GetMovieDetailAsync(string subjectId, CancellationToken ct)
        {
            return GetDetailCoreAsync(MovieDetailEndpointFormat, subjectId, ct);
        }

        public Task<DoubanDetailResponse> GetTvDetailAsync(string subjectId, CancellationToken ct)
        {
            return GetDetailCoreAsync(TvDetailEndpointFormat, subjectId, ct);
        }

        public Task<DoubanSearchResponse> SearchAsync(string keyword, string type, CancellationToken ct)
        {
            var url = string.Format(SearchEndpointFormat,
                Uri.EscapeDataString(keyword ?? string.Empty),
                Uri.EscapeDataString(string.IsNullOrWhiteSpace(type) ? "movie" : type));
            // The search endpoint requires the /search/ page as Referer (calibrated 2026-10-29):
            // the bare origin returns 400 invalid_request_1284.
            return GetJsonAsync<DoubanSearchResponse>(url, referer: "https://m.douban.com/search/", ct);
        }

        private Task<DoubanDetailResponse> GetDetailCoreAsync(string endpointFormat, string subjectId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return Task.FromResult<DoubanDetailResponse>(null);
            }

            var normalized = subjectId.Trim();
            var url = string.Format(endpointFormat, Uri.EscapeDataString(normalized));
            // A missing or mismatched Referer makes the rexxar API answer 400.
            var referer = endpointFormat == TvDetailEndpointFormat
                ? "https://m.douban.com/tv/subject/" + normalized + "/"
                : "https://m.douban.com/movie/subject/" + normalized + "/";
            return GetJsonAsync<DoubanDetailResponse>(url, referer, ct);
        }

        /// <summary>
        ///     GET + JSON deserialize with throttling, 15s linked-CTS timeout and full
        ///     exception swallowing. Cancellation of the caller's token propagates.
        /// </summary>
        private async Task<T> GetJsonAsync<T>(string url, string referer, CancellationToken ct)
            where T : class
        {
            try
            {
                await ThrottleAsync(ct).ConfigureAwait(false);
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
                    var options = new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = timeoutCts.Token,
                        AcceptHeader = "application/json",
                        BufferContent = true,
                        UserAgent = MobileUserAgent,
                        EnableDefaultUserAgent = false,
                        Referer = referer
                    };

                    using (var response = await _httpClient.SendAsync(options, "GET").ConfigureAwait(false))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            _logger?.Debug("Douban - HTTP {0} from {1}", (int)response.StatusCode, url);
                            return null;
                        }

                        using (var stream = response.Content)
                        {
                            return _jsonSerializer.DeserializeFromStream<T>(stream);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger?.Debug("Douban - Request failed for {0}: {1}", url, e.Message);
                return null;
            }
        }

        private static async Task ThrottleAsync(CancellationToken ct)
        {
            int waitMs;
            lock (ThrottleLock)
            {
                var now = DateTimeOffset.UtcNow.Ticks;
                if (_lastRequestTicks <= 0)
                {
                    waitMs = 0;
                }
                else
                {
                    var elapsedMs = (now - _lastRequestTicks) / TimeSpan.TicksPerMillisecond;
                    waitMs = elapsedMs >= RequestIntervalMs
                        ? 0
                        : (int)Math.Min(RequestIntervalMs, RequestIntervalMs - Math.Max(0, elapsedMs));
                }

                _lastRequestTicks = now;
            }

            if (waitMs > 0)
            {
                await Task.Delay(waitMs, ct).ConfigureAwait(false);
            }
        }
    }
}

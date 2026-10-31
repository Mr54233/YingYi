using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Rating.Dto;
using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Rating
{
    /// <summary>
    /// Resolves an IMDb id to a Douban subject id through the WikiData Query Service.
    /// WikiData is used strictly as an identifier bridge here; it is never a rating source.
    /// </summary>
    public class WikiDataClient
    {
        private const string SparqlEndpoint = "https://query.wikidata.org/sparql";
        private const int RequestIntervalMs = 1000;
        // NAS 实测（2026-10-31）：WikiData 对家庭宽带 IP 波动极大（1s/24s/60s 不等），
        // 15s 超时导致大部分解析 unresolved。提到 60s 并加重试。
        private const int RequestTimeoutSeconds = 60;
        private const int ResolveRetryCount = 2;

        // CALIBRATED 2026-10-29 against live WikiData: tt0111161 (The Shawshank Redemption)
        // -> P4529 = "1292052", which is the correct Douban subject id for 肖申克的救赎.
        // Keep this a single constant so it can be corrected in one place if WikiData changes.
        private const string DoubanPropertyId = "P4529";

        // P345 (IMDb id) literals are stored with the "tt" prefix (e.g. "tt0133093").
        // Braces are doubled because this template is consumed by string.Format.
        private const string SparqlQueryTemplate = @"SELECT ?doubanId WHERE {{
  ?item wdt:P345 ""{0}"" .
  ?item wdt:{1} ?doubanId .
}}
LIMIT 1";

        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private static readonly object SyncRoot = new object();
        private static long _lastRequestTicks;

        public WikiDataClient(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Returns the Douban subject id bound to the given IMDb id, or null when unavailable.
        /// Never throws: every failure (bad id, HTTP error, cancellation, malformed JSON) is
        /// swallowed and logged at Debug level.
        /// </summary>
        public async Task<string> ResolveDoubanIdAsync(string imdbId, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imdbId))
                {
                    return null;
                }

                var normalized = imdbId.Trim();
                if (!normalized.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = "tt" + normalized;
                }

                if (normalized.Length <= 2)
                {
                    return null;
                }

                var query = string.Format(CultureInfo.InvariantCulture, SparqlQueryTemplate,
                    EscapeSparqlLiteral(normalized), DoubanPropertyId);
                var url = SparqlEndpoint + "?format=json&query=" + Uri.EscapeDataString(query);

                for (var attempt = 0; attempt <= ResolveRetryCount; attempt++)
                {
                    if (attempt > 0)
                    {
                        // WikiData 会间歇性挂起；失败后等待一段时间再重试。
                        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                    }

                    await ThrottleAsync(RequestIntervalMs, ct).ConfigureAwait(false);

                    using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

                        var options = new HttpRequestOptions
                        {
                            Url = url,
                            CancellationToken = timeoutCts.Token,
                            AcceptHeader = "application/json",
                            BufferContent = true,
                            UserAgent = Plugin.Instance.UserAgent,
                            EnableDefaultUserAgent = false,
                            // Emby HttpClient 的 EnableAutomaticTimeouts 默认会按 URL 推断一个
                            // 约 20s 的超时并覆盖外层 CancelAfter（真机日志实测 20s timeout）。
                            // 显式关掉自动超时、用 TimeoutMs 对齐 60s。
                            EnableAutomaticTimeouts = false,
                            TimeoutMs = RequestTimeoutSeconds * 1000
                        };

                        using (var response = await _httpClient.SendAsync(options, "GET").ConfigureAwait(false))
                        {
                            if (response.StatusCode != HttpStatusCode.OK)
                            {
                                _logger.Debug("WikiData - HTTP {0} while resolving IMDb id {1} (attempt {2})",
                                    (int)response.StatusCode, imdbId, attempt + 1);
                                continue;
                            }

                            using (var stream = response.Content)
                            {
                                var result = _jsonSerializer.DeserializeFromStream<WikiDataResponse>(stream);
                                var bindings = result?.results?.bindings;
                                if (bindings == null || bindings.Count == 0)
                                {
                                    // 空绑定通常意味着 Wikidata 里确实没有该条目，重试无意义。
                                    return null;
                                }

                                for (var i = 0; i < bindings.Count; i++)
                                {
                                    var row = bindings[i];
                                    if (row != null && row.TryGetValue("doubanId", out var binding) &&
                                        binding != null && !string.IsNullOrWhiteSpace(binding.value))
                                    {
                                        return binding.value.Trim();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Debug("WikiData - Failed to resolve Douban id for {0}: {1}", imdbId, e.Message);
            }

            return null;
        }

        private static string EscapeSparqlLiteral(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// Shared static rate limiter (same contract as CommunityIntroSource.ThrottleAsync):
        /// the wait is computed inside the lock, the delay is awaited outside it, and the very
        /// first request is a zero-wait fast path so _lastRequestTicks == 0 cannot overflow int.
        /// </summary>
        private static async Task ThrottleAsync(int intervalMs, CancellationToken cancellationToken)
        {
            if (intervalMs <= 0)
            {
                return;
            }

            int waitMs;
            lock (SyncRoot)
            {
                var now = DateTimeOffset.UtcNow.Ticks;
                if (_lastRequestTicks <= 0)
                {
                    waitMs = 0;
                }
                else
                {
                    var elapsedMs = (now - _lastRequestTicks) / TimeSpan.TicksPerMillisecond;
                    waitMs = elapsedMs >= intervalMs
                        ? 0
                        : (int)Math.Min(intervalMs, Math.Max(0, intervalMs - elapsedMs));
                }

                _lastRequestTicks = now;
            }

            if (waitMs > 0)
            {
                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

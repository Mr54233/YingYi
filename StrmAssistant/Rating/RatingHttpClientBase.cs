using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Rating
{
    public abstract class RatingHttpClientBase
    {
        private static readonly object ThrottleLock = new object();
        private static long _lastRequestTicks;
        private const int RequestIntervalMs = 1000;
        private const int TimeoutSeconds = 15;

        protected readonly ILogger Logger;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;

        protected RatingHttpClientBase(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            Logger = logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
        }

        protected async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                await ThrottleAsync(cancellationToken).ConfigureAwait(false);
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
                    var request = new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = timeoutCts.Token,
                        AcceptHeader = "application/json",
                        BufferContent = true,
                        UserAgent = Plugin.Instance?.UserAgent ?? "YingYi",
                        EnableDefaultUserAgent = false
                    };

                    using (var response = await _httpClient.SendAsync(request, "GET").ConfigureAwait(false))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            Logger?.Debug("Rating - HTTP {0} from {1}", (int)response.StatusCode, url);
                            return null;
                        }

                        using (var stream = response.Content)
                        {
                            return _jsonSerializer.DeserializeFromStream<T>(stream);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger?.Debug("Rating - Request failed for {0}: {1}", url, e.Message);
                return null;
            }
        }

        private static async Task ThrottleAsync(CancellationToken cancellationToken)
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
                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

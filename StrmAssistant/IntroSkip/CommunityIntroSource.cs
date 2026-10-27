using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.IntroSkip
{
    /// <summary>
    /// Result of a community intro-source lookup for one episode.
    /// </summary>
    public class CommunityIntroResult
    {
        public long? IntroStartTicks { get; set; }
        public long? IntroEndTicks { get; set; }
        public long? CreditsStartTicks { get; set; }
        public double? Confidence { get; set; }
        public string Source { get; set; }
        public bool ConfirmedNoIntro { get; set; }
    }

    /// <summary>
    /// Negative-result cache entry: internalId -> verdict + timestamp.
    /// </summary>
    public class CommunityIntroCacheEntry
    {
        public long InternalId { get; set; }
        public long CheckedAtUnix { get; set; }
        public bool NoIntro { get; set; }
        public bool NoData { get; set; }
        public bool HasIntro { get; set; }
        public double? Confidence { get; set; }
        public string Source { get; set; }
    }

    public class CommunityIntroCacheFile
    {
        public int Version { get; set; } = 1;
        public List<CommunityIntroCacheEntry> Entries { get; set; } = new List<CommunityIntroCacheEntry>();
    }

    /// <summary>
    /// Query client for community intro sources (IntroDB A source; TheIntroDB B source Phase 2).
    /// Rate-limited, all exceptions swallowed; every lookup failure degrades to "no data".
    /// </summary>
    public class CommunityIntroSource
    {
        private const string IntroDbBaseUrl = "https://api.introdb.app";
        private const string TheIntroDbBaseUrl = "https://api.theintrodb.org/v4";
        private const string CacheFileName = "community_intro_skip.json";

        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IFileSystem _fileSystem;
        private readonly IItemRepository _itemRepository;

        private static readonly object SyncRoot = new object();
        private static long _lastRequestTicks;
        private CommunityIntroCacheFile _cache;
        private bool _cacheLoaded;

        public CommunityIntroSource(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer,
            IFileSystem fileSystem, IItemRepository itemRepository)
        {
            _logger = logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            _itemRepository = itemRepository;
        }

        // -------- DTOs: IntroDB (A source) --------
        // unknown imdb_id returns 200 with all-null segments; every field must be nullable
        public class IntroDbSegmentsResponse
        {
            public string imdb_id { get; set; }
            public string media_type { get; set; }
            public bool? is_movie { get; set; }
            public int? season { get; set; }
            public int? episode { get; set; }
            public IntroDbSegment intro { get; set; }
            public IntroDbSegment recap { get; set; }
            public IntroDbSegment outro { get; set; }
            public IntroDbSegment post_credits { get; set; }
            public bool? no_intro { get; set; }
            public bool? no_recap { get; set; }
            public bool? no_outro { get; set; }
        }

        public class IntroDbSegment
        {
            public double? start_sec { get; set; }
            public double? end_sec { get; set; }
            public int? start_ms { get; set; }
            public int? end_ms { get; set; }
            public double? confidence { get; set; }
            public int? submission_count { get; set; }
            public string updated_at { get; set; }
        }

        // -------- DTOs: TheIntroDB (B source, Phase 2) --------
        public class TheIntroDbMediaResponse
        {
            public string kind { get; set; }
            public string tvdb_id { get; set; }
            public string imdb_id { get; set; }
            public int? season { get; set; }
            public int? episode { get; set; }
            public List<TheIntroDbSegment> intro { get; set; }
            public List<TheIntroDbSegment> recap { get; set; }
            public List<TheIntroDbSegment> credits { get; set; }
            public List<TheIntroDbSegment> preview { get; set; }
        }

        public class TheIntroDbSegment
        {
            public int? start_ms { get; set; }
            public int? end_ms { get; set; }
            public double? confidence { get; set; }
            public int? submission_count { get; set; }
        }

        public string GetImdbId(Episode episode)
        {
            try
            {
                if (episode.ProviderIds != null && episode.ProviderIds.TryGetValue("Imdb", out var imdb) &&
                    !string.IsNullOrWhiteSpace(imdb))
                {
                    return imdb;
                }

                var series = episode.Series;
                if (series != null && series.ProviderIds != null &&
                    series.ProviderIds.TryGetValue("Imdb", out var seriesImdb) &&
                    !string.IsNullOrWhiteSpace(seriesImdb))
                {
                    return seriesImdb;
                }
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Failed to resolve IMDb id for {0}: {1}", episode.Name, e.Message);
            }

            return null;
        }

        public string GetTvdbId(Episode episode)
        {
            try
            {
                if (episode.ProviderIds != null && episode.ProviderIds.TryGetValue("Tvdb", out var tvdb) &&
                    !string.IsNullOrWhiteSpace(tvdb))
                {
                    return tvdb;
                }

                var series = episode.Series;
                if (series != null && series.ProviderIds != null &&
                    series.ProviderIds.TryGetValue("Tvdb", out var seriesTvdb) &&
                    !string.IsNullOrWhiteSpace(seriesTvdb))
                {
                    return seriesTvdb;
                }
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Failed to resolve TVDB id for {0}: {1}", episode.Name, e.Message);
            }

            return null;
        }

        /// <summary>
        /// Look up community intro data for one episode across enabled sources.
        /// Returns null when no source produced usable data (all failures swallowed).
        /// </summary>
        public CommunityIntroResult Lookup(Episode episode, bool enableIntroDb, bool enableTheIntroDb,
            int timeoutSeconds, double minConfidence, CancellationToken cancellationToken)
        {
            var result = TryIntroDb(episode, enableIntroDb, timeoutSeconds, minConfidence, cancellationToken);

            if (result == null && enableTheIntroDb)
            {
                result = TryTheIntroDb(episode, timeoutSeconds, minConfidence, cancellationToken);
            }

            return result;
        }

        private CommunityIntroResult TryIntroDb(Episode episode, bool enabled, int timeoutSeconds, double minConfidence,
            CancellationToken cancellationToken)
        {
            if (!enabled)
            {
                return null;
            }

            var imdb = GetImdbId(episode);
            if (string.IsNullOrWhiteSpace(imdb))
            {
                _logger.Debug("CommunityIntro - No IMDb id, skip IntroDB for {0}", episode.Name);
                return null;
            }

            if (episode.ParentIndexNumber == null || episode.IndexNumber == null)
            {
                _logger.Debug("CommunityIntro - Missing season/episode number, skip IntroDB for {0}", episode.Name);
                return null;
            }

            var url = $"{IntroDbBaseUrl}/segments?imdb_id={Uri.EscapeDataString(imdb)}" +
                      $"&season={episode.ParentIndexNumber}&episode={episode.IndexNumber}";

            var response = GetJson<IntroDbSegmentsResponse>(url, timeoutSeconds, cancellationToken);
            if (response == null)
            {
                return null;
            }

            var intro = response.intro;
            var outro = response.outro;

            long? creditsStart = null;
            if (outro != null)
            {
                var outroTicks = SegmentToTicks(outro.start_ms, outro.end_ms, outro.start_sec, outro.end_sec);
                var outroConfidence = outro.confidence ?? 0;
                var runtime = episode.RunTimeTicks;
                if (outroTicks != null && runtime.HasValue && outroConfidence >= minConfidence)
                {
                    // credits start is derived backwards from the runtime, same as UpdateCredits
                    var creditsDuration = outroTicks.Value.end - outroTicks.Value.start;
                    if (creditsDuration >= 5 * TimeSpan.TicksPerSecond &&
                        creditsDuration <= 900 * TimeSpan.TicksPerSecond &&
                        runtime.Value - creditsDuration > 0)
                    {
                        creditsStart = runtime.Value - creditsDuration;
                    }
                }
            }

            if (intro != null)
            {
                var ticks = SegmentToTicks(intro.start_ms, intro.end_ms, intro.start_sec, intro.end_sec);
                var confidence = intro.confidence ?? 0;
                if (ticks != null && confidence >= minConfidence)
                {
                    return new CommunityIntroResult
                    {
                        IntroStartTicks = ticks.Value.start,
                        IntroEndTicks = ticks.Value.end,
                        CreditsStartTicks = creditsStart,
                        Confidence = confidence,
                        Source = "IntroDb"
                    };
                }

                _logger.Debug("CommunityIntro - IntroDB segment rejected (range null or confidence {0} < {1}) for {2}",
                    confidence, minConfidence, episode.Name);
                return null;
            }

            if (response.no_intro == true)
            {
                return new CommunityIntroResult
                {
                    ConfirmedNoIntro = true,
                    CreditsStartTicks = creditsStart,
                    Source = "IntroDb"
                };
            }

            return null;
        }

        private CommunityIntroResult TryTheIntroDb(Episode episode, int timeoutSeconds, double minConfidence,
            CancellationToken cancellationToken)
        {
            if (episode.ParentIndexNumber == null || episode.IndexNumber == null)
            {
                _logger.Debug("CommunityIntro - Missing season/episode number, skip TheIntroDB for {0}", episode.Name);
                return null;
            }

            var tvdb = GetTvdbId(episode);
            string url;
            if (!string.IsNullOrWhiteSpace(tvdb))
            {
                url = $"{TheIntroDbBaseUrl}/media?tvdb_id={Uri.EscapeDataString(tvdb)}" +
                      $"&season={episode.ParentIndexNumber}&episode={episode.IndexNumber}";
            }
            else
            {
                var imdb = GetImdbId(episode);
                if (string.IsNullOrWhiteSpace(imdb))
                {
                    _logger.Debug("CommunityIntro - No IMDb id, skip TheIntroDB for {0}", episode.Name);
                    return null;
                }

                url = $"{TheIntroDbBaseUrl}/media?imdb_id={Uri.EscapeDataString(imdb)}" +
                      $"&season={episode.ParentIndexNumber}&episode={episode.IndexNumber}";
            }

            var response = GetJson<TheIntroDbMediaResponse>(url, timeoutSeconds, cancellationToken);
            if (response == null)
            {
                return null;
            }

            if (response.kind == "media" && response.intro != null && response.intro.Count > 0)
            {
                // multiple segments legal: take highest submission_count, no union
                var best = response.intro
                    .OrderByDescending(s => s.submission_count ?? 0)
                    .ThenByDescending(s => s.confidence ?? 0)
                    .First();
                var ticks = SegmentToTicks(best.start_ms, best.end_ms, null, null);
                var confidence = best.confidence ?? 0;
                if (ticks != null && confidence >= minConfidence)
                {
                    return new CommunityIntroResult
                    {
                        IntroStartTicks = ticks.Value.start,
                        IntroEndTicks = ticks.Value.end,
                        Confidence = confidence,
                        Source = "TheIntroDb"
                    };
                }

                _logger.Debug("CommunityIntro - TheIntroDB segment rejected for {0}", episode.Name);
            }

            return null;
        }

        private static (long start, long end)? SegmentToTicks(int? startMs, int? endMs, double? startSec, double? endSec)
        {
            long? start = null;
            long? end = null;

            if (startMs.HasValue && endMs.HasValue)
            {
                start = startMs.Value * TimeSpan.TicksPerMillisecond;
                end = endMs.Value * TimeSpan.TicksPerMillisecond;
            }
            else if (startSec.HasValue && endSec.HasValue)
            {
                start = (long)(startSec.Value * TimeSpan.TicksPerSecond);
                end = (long)(endSec.Value * TimeSpan.TicksPerSecond);
            }

            if (start == null || end == null || end.Value <= start.Value)
            {
                return null;
            }

            return (start.Value, end.Value);
        }

        /// <summary>
        /// HTTP GET returning deserialized JSON, or null on any failure (non-200, timeout, parse error).
        /// Timeout enforced via CancellationTokenSource, never HttpRequestOptions.TimeoutMs.
        /// </summary>
        private T GetJson<T>(string url, int timeoutSeconds, CancellationToken cancellationToken) where T : class
        {
            try
            {
                Throttle(Plugin.Instance.GetPluginOptions().IntroSkipOptions.CommunitySourceRequestIntervalMs);

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    var options = new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = timeoutCts.Token,
                        AcceptHeader = "application/json",
                        BufferContent = true,
                        UserAgent = Plugin.Instance.UserAgent,
                        EnableDefaultUserAgent = false
                    };

                    using (var response = _httpClient.SendAsync(options, "GET").GetAwaiter().GetResult())
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            _logger.Debug("CommunityIntro - HTTP {0} from {1}", (int)response.StatusCode, url);
                            return null;
                        }

                        using (var stream = response.Content)
                        {
                            return _jsonSerializer.DeserializeFromStream<T>(stream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Request failed for {0}: {1}", url, e.Message);
                return null;
            }
        }

        /// <summary>
        /// Simple static rate limiter shared across source clients (metadata-api pattern).
        /// </summary>
        private static void Throttle(int intervalMs)
        {
            if (intervalMs <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                var elapsed = DateTimeOffset.UtcNow.Ticks - _lastRequestTicks;
                var waitMs = intervalMs - (int)(elapsed / TimeSpan.TicksPerMillisecond);
                if (waitMs > 0)
                {
                    Thread.Sleep(waitMs);
                }

                _lastRequestTicks = DateTimeOffset.UtcNow.Ticks;
            }
        }

        // -------- Negative / verdict cache --------

        private string CacheFilePath =>
            Path.Combine(Plugin.Instance.ApplicationPaths.PluginConfigurationsPath, CacheFileName);

        private CommunityIntroCacheFile Cache
        {
            get
            {
                if (_cacheLoaded)
                {
                    return _cache;
                }

                lock (SyncRoot)
                {
                    if (_cacheLoaded)
                    {
                        return _cache;
                    }

                    _cache = new CommunityIntroCacheFile();
                    try
                    {
                        if (_fileSystem.FileExists(CacheFilePath))
                        {
                            _cache = _jsonSerializer.DeserializeFromFile<CommunityIntroCacheFile>(CacheFilePath) ??
                                     new CommunityIntroCacheFile();
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Debug("CommunityIntro - Failed to load cache: {0}", e.Message);
                        _cache = new CommunityIntroCacheFile();
                    }

                    _cacheLoaded = true;
                    return _cache;
                }
            }
        }

        public bool TryGetCachedVerdict(long internalId, int cacheDays, out CommunityIntroCacheEntry entry)
        {
            entry = null;
            try
            {
                lock (SyncRoot)
                {
                    var found = Cache.Entries.FirstOrDefault(e => e.InternalId == internalId);
                    if (found == null)
                    {
                        return false;
                    }

                    var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - found.CheckedAtUnix;
                    if (age > cacheDays * 24L * 3600L)
                    {
                        Cache.Entries.Remove(found);
                        return false;
                    }

                    entry = found;
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Cache read failed: {0}", e.Message);
                return false;
            }
        }

        public void SaveCacheVerdict(long internalId, CommunityIntroResult result)
        {
            try
            {
                lock (SyncRoot)
                {
                    var existing = Cache.Entries.FirstOrDefault(e => e.InternalId == internalId);
                    if (existing != null)
                    {
                        Cache.Entries.Remove(existing);
                    }

                    Cache.Entries.Add(new CommunityIntroCacheEntry
                    {
                        InternalId = internalId,
                        CheckedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        NoIntro = result?.ConfirmedNoIntro == true,
                        NoData = result == null,
                        HasIntro = result?.IntroStartTicks != null,
                        Confidence = result?.Confidence,
                        Source = result?.Source
                    });

                    PersistCache();
                }
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Cache write failed: {0}", e.Message);
            }
        }

        private void PersistCache()
        {
            try
            {
                var directory = _fileSystem.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    _fileSystem.CreateDirectory(directory);
                }

                _jsonSerializer.SerializeToFile(_cache, CacheFilePath);
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Failed to persist cache: {0}", e.Message);
            }
        }

        // -------- Marker write --------

        /// <summary>
        /// Write intro/credits markers reusing the ChapterApi "#SA" convention.
        /// Idempotent: skips write when markers already exist with the same values.
        /// </summary>
        public void WriteMarkers(Episode episode, CommunityIntroResult result)
        {
            try
            {
                var chapters = _itemRepository.GetChapters(episode) ?? new List<ChapterInfo>();
                var hasIntro = chapters.Any(c => c.MarkerType == MarkerType.IntroStart);
                var changed = false;

                if (result.IntroStartTicks != null && result.IntroEndTicks != null)
                {
                    var durationTicks = result.IntroEndTicks.Value - result.IntroStartTicks.Value;
                    var runtime = episode.RunTimeTicks;
                    if (runtime.HasValue && durationTicks >= 5 * TimeSpan.TicksPerSecond &&
                        durationTicks <= 180 * TimeSpan.TicksPerSecond && result.IntroEndTicks.Value < runtime.Value)
                    {
                        if (!hasIntro)
                        {
                            chapters.RemoveAll(c => c.MarkerType == MarkerType.IntroStart || c.MarkerType == MarkerType.IntroEnd);
                            chapters.Add(new ChapterInfo
                            {
                                Name = MarkerType.IntroStart + ChapterApi.MarkerSuffix,
                                MarkerType = MarkerType.IntroStart,
                                StartPositionTicks = result.IntroStartTicks.Value
                            });
                            chapters.Add(new ChapterInfo
                            {
                                Name = MarkerType.IntroEnd + ChapterApi.MarkerSuffix,
                                MarkerType = MarkerType.IntroEnd,
                                StartPositionTicks = result.IntroEndTicks.Value
                            });
                            _logger.Info("CommunityIntro - Wrote intro markers {0}-{1}s for {2}",
                                result.IntroStartTicks.Value / TimeSpan.TicksPerSecond,
                                result.IntroEndTicks.Value / TimeSpan.TicksPerSecond, episode.Name);
                            changed = true;
                        }
                        else
                        {
                            _logger.Debug("CommunityIntro - Intro marker already present, skip {0}", episode.Name);
                        }
                    }
                    else
                    {
                        _logger.Debug("CommunityIntro - Intro range out of bounds, skip {0}", episode.Name);
                    }
                }

                if (result.CreditsStartTicks != null && !chapters.Any(c => c.MarkerType == MarkerType.CreditsStart))
                {
                    chapters.Add(new ChapterInfo
                    {
                        Name = MarkerType.CreditsStart + ChapterApi.MarkerSuffix,
                        MarkerType = MarkerType.CreditsStart,
                        StartPositionTicks = result.CreditsStartTicks.Value
                    });
                    _logger.Info("CommunityIntro - Wrote credits marker at {0}s for {1}",
                        result.CreditsStartTicks.Value / TimeSpan.TicksPerSecond, episode.Name);
                    changed = true;
                }

                if (!changed)
                {
                    return;
                }

                chapters.Sort((a, b) => a.StartPositionTicks.CompareTo(b.StartPositionTicks));
                _itemRepository.SaveChapters(episode.InternalId, chapters);
            }
            catch (Exception e)
            {
                _logger.Debug("CommunityIntro - Failed to write markers for {0}: {1}", episode.Name, e.Message);
            }
        }
    }
}

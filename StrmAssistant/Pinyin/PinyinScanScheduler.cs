using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using StrmAssistant.Options;

namespace StrmAssistant.Pinyin
{
    /// <summary>
    ///     Owns the three timing layers required by the plan (§3.2):
    ///     1. catch-up run 60s after the plugin finished loading,
    ///     2. an <see cref="ILibraryPostScanTask"/> hook plus a 5 minute incremental timer,
    ///     3. an event queue (ItemAdded / ItemUpdated / RefreshCompleted) drained by a
    ///        30 second timer in batches.
    ///     Nothing here may block the plugin constructor or Run(): every entry point only
    ///     starts a background worker with Task.Run and waits with Thread.Sleep, because
    ///     Task.Delay deadlocks on the single threaded synchronization context of 4.9.5.0.
    /// </summary>
    internal sealed class PinyinScanScheduler : IDisposable
    {
        internal const string CursorFileName = "pinyin-cursor.json";

        private const int CatchUpDelayMs = 60 * 1000;
        private const int EventTimerPeriodMs = 30 * 1000;
        private const int IncrementalTimerPeriodMs = 5 * 60 * 1000;
        private const int EventBatchSize = 200;

        private readonly ILogger _logger;
        private readonly EmbyDatabase _database;
        private readonly FtsIndexWriter _ftsWriter;
        private readonly PinyinSortNameService _sortNameService;

        private readonly ConcurrentQueue<long> _pendingItems = new ConcurrentQueue<long>();
        private readonly object _sync = new object();

        private Timer _eventTimer;
        private Timer _incrementalTimer;
        private CancellationTokenSource _catchUpCts;
        private bool _started;
        private bool _disposed;

        internal PinyinScanScheduler(ILogger logger, EmbyDatabase database, ILibraryManager libraryManager,
            string programDataPath)
        {
            _logger = logger;
            _database = database;

            var cursorDirectory = Path.Combine(programDataPath, "plugins", "影医");
            var cursorFilePath = Path.Combine(cursorDirectory, CursorFileName);
            try
            {
                Directory.CreateDirectory(cursorDirectory);
            }
            catch (Exception ex)
            {
                _logger.Info("PinyinScheduler - cannot create {0} ({1})", cursorDirectory, ex.Message);
            }

            _ftsWriter = new FtsIndexWriter(logger, database, cursorFilePath);
            _sortNameService = new PinyinSortNameService(logger, database, libraryManager);
        }

        internal PinyinSortNameService SortNameService => _sortNameService;

        internal FtsIndexWriter FtsWriter => _ftsWriter;

        internal void Start()
        {
            lock (_sync)
            {
                if (_disposed || _started) return;
                _started = true;
                _catchUpCts = new CancellationTokenSource();
            }

            _logger.Info("PinyinScheduler - starting (catch-up in {0}s)", CatchUpDelayMs / 1000);

            try
            {
                _eventTimer = new Timer(OnEventTimer, null, EventTimerPeriodMs, EventTimerPeriodMs);
                _incrementalTimer = new Timer(OnIncrementalTimer, null, IncrementalTimerPeriodMs,
                    IncrementalTimerPeriodMs);
            }
            catch (Exception ex)
            {
                _logger.Error("PinyinScheduler - failed to start timers: {0}", ex.Message);
            }

            var token = _catchUpCts.Token;

            // Never Task.Delay here: 4.9.5.0 runs plugin code on a single threaded
            // synchronization context and a Delay continuation can deadlock. The catch-up
            // run is fully detached from the plugin constructor.
            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(CatchUpDelayMs);
                    if (token.IsCancellationRequested) return;

                    CatchUp(token);
                }
                catch (Exception ex)
                {
                    _logger.Error("PinyinScheduler - catch-up failed: {0}", ex.Message);
                }
            });
        }

        internal void Stop()
        {
            lock (_sync)
            {
                if (!_started) return;
                _started = false;
            }

            try
            {
                _catchUpCts?.Cancel();
            }
            catch
            {
                // ignore
            }

            DisposeTimers();

            while (_pendingItems.TryDequeue(out _))
            {
                // drain the queue so a later Start() begins clean
            }

            _logger.Info("PinyinScheduler - stopped");
        }

        /// <summary>Called from the plugin event handlers; must stay non blocking.</summary>
        internal void Enqueue(long internalId)
        {
            if (internalId <= 0) return;

            lock (_sync)
            {
                if (!_started) return;
            }

            _pendingItems.Enqueue(internalId);
        }

        /// <summary>Called by the post scan task right after a library scan finished.</summary>
        internal void OnLibraryScanCompleted()
        {
            lock (_sync)
            {
                if (!_started) return;
            }

            Task.Run(() =>
            {
                try
                {
                    RunIncremental(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.Error("PinyinScheduler - post scan incremental failed: {0}", ex.Message);
                }
            });
        }

        private void CatchUp(CancellationToken token)
        {
            var options = GetOptions();
            if (options == null) return;

            if (options.EnablePinyinSearch)
            {
                _logger.Info("PinyinScheduler - catch-up injection started");
                var count = _ftsWriter.InjectPending(null, token);
                _logger.Info("PinyinScheduler - catch-up injection done ({0} items)", count);
            }

            if (token.IsCancellationRequested) return;

            if (options.EnablePinyinSortName)
            {
                _logger.Info("PinyinScheduler - catch-up sort name started");
                var count = _sortNameService.ApplyPending(null, token);
                _logger.Info("PinyinScheduler - catch-up sort name done ({0} items)", count);
            }
        }

        private void OnEventTimer(object state)
        {
            try
            {
                RunIncremental(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Debug("PinyinScheduler - event timer failed: {0}", ex.Message);
            }
        }

        private void OnIncrementalTimer(object state)
        {
            try
            {
                var options = GetOptions();
                if (options == null) return;
                if (!options.EnablePinyinSearch && !options.EnablePinyinSortName) return;

                RunIncremental(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Debug("PinyinScheduler - incremental timer failed: {0}", ex.Message);
            }
        }

        private void RunIncremental(CancellationToken token)
        {
            var options = GetOptions();
            if (options == null) return;
            if (!options.EnablePinyinSearch && !options.EnablePinyinSortName) return;

            DrainEventQueue(options, token);

            if (options.EnablePinyinSearch) _ftsWriter.InjectPending(null, token);
            if (options.EnablePinyinSortName) _sortNameService.ApplyPending(null, token);
        }

        private void DrainEventQueue(PinyinEnhanceOptions options, CancellationToken token)
        {
            var processed = 0;

            while (processed < EventBatchSize && !token.IsCancellationRequested)
            {
                if (!_pendingItems.TryDequeue(out var internalId)) break;
                processed++;

                try
                {
                    if (options.EnablePinyinSearch) _ftsWriter.InjectItem(internalId);
                    if (options.EnablePinyinSortName) _sortNameService.ApplyItem(internalId);
                }
                catch (Exception ex)
                {
                    _logger.Debug("PinyinScheduler - event item {0} failed: {1}", internalId, ex.Message);
                }
            }

            if (processed > 0) _logger.Debug("PinyinScheduler - processed {0} queued items", processed);
        }

        private static PinyinEnhanceOptions GetOptions()
        {
            try
            {
                return Plugin.Instance?.GetPluginOptions()?.PinyinEnhanceOptions;
            }
            catch
            {
                return null;
            }
        }

        private void DisposeTimers()
        {
            try
            {
                _eventTimer?.Dispose();
            }
            catch
            {
                // ignore
            }

            try
            {
                _incrementalTimer?.Dispose();
            }
            catch
            {
                // ignore
            }

            _eventTimer = null;
            _incrementalTimer = null;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }

            Stop();
        }
    }
}

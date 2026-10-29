using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Common;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static StrmAssistant.Options.MediaInfoExtractOptions;

namespace StrmAssistant.ScheduledTask
{
    public class ExtractVideoThumbnailTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;

        public ExtractVideoThumbnailTask(IFileSystem fileSystem, ILibraryManager libraryManager,
            IItemRepository itemRepository)
        {
            _logger = Plugin.Instance.Logger;
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("VideoThumbnailExtract - Scheduled Task Execute");

            var maxConcurrentCount = Plugin.Instance.GetPluginOptions().GeneralOptions.MaxConcurrentCount;
            _logger.Info("Master Max Concurrent Count: " + maxConcurrentCount);
            var cooldownSeconds = maxConcurrentCount == 1
                ? Plugin.Instance.GetPluginOptions().GeneralOptions.CooldownDurationSeconds
                : (int?)null;
            if (cooldownSeconds.HasValue) _logger.Info("Cooldown Duration Seconds: " + cooldownSeconds.Value);

            var persistMediaInfoMode = Plugin.Instance.GetPluginOptions().MediaInfoExtractOptions.PersistMediaInfoMode;
            _logger.Info("Persist MediaInfo Mode: " + persistMediaInfoMode);
            var persistMediaInfo = persistMediaInfoMode != PersistMediaInfoOption.None.ToString();
            var mediaInfoRestoreMode = persistMediaInfoMode == PersistMediaInfoOption.Restore.ToString();

            var items = Plugin.VideoThumbnailApi.FetchExtractTaskItems();
            _logger.Info($"VideoThumbnailExtract - Number of items: {items.Count}");

            if (items.Count > 0) IsRunning = true;

            var directoryService = new DirectoryService(_logger, _fileSystem);

            double total = items.Count;
            var index = 0;
            var current = 0;
            var skip = 0;

            var tasks = new List<Task>();

            foreach (var item in items)
            {
                try
                {
                    await QueueManager.MasterSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    QueueManager.MasterSemaphore.Release();
                    _logger.Info("VideoThumbnailExtract - Scheduled Task Cancelled");
                    return;
                }

                var taskIndex = ++index;
                var taskItem = item;
                var task = Task.Run(async () =>
                {
                    var result = false;

                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.Info("VideoThumbnailExtract - Scheduled Task Cancelled");
                            return;
                        }

                        var chapters = _itemRepository.GetChapters(taskItem);

                        var thumbnailResult = false;

                        if (persistMediaInfo)
                        {
                            thumbnailResult = await Plugin.MediaInfoApi.DeserializeChapterInfo(taskItem, chapters,
                                directoryService, "VideoThumbnailExtract Task").ConfigureAwait(false);
                        }

                        if (!thumbnailResult)
                        {
                            if (!mediaInfoRestoreMode)
                            {
                                var libraryOptions = _libraryManager.GetLibraryOptions(taskItem);
                                result = await Plugin.VideoThumbnailApi
                                    .RefreshThumbnailImages(taskItem, libraryOptions, directoryService, chapters, true,
                                        true, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                Interlocked.Increment(ref skip);
                            }
                        }

                        // Pro: ImageCaptureOffsetSeconds post-process. Semantics note: this
                        // plugin has no percentage-based frame capture; Emby only extracts
                        // chapter images, so the offset is applied to each chapter's capture
                        // position on a throw-away copy (clamped to 0..RunTimeTicks). The
                        // generated images are written back to the real chapters afterwards.
                        var extractOptions = Plugin.Instance.GetPluginOptions().MediaInfoExtractOptions;
                        var offsetSeconds = extractOptions.EnableImageCapturePostProcess
                            ? extractOptions.ImageCaptureOffsetSeconds
                            : 0;
                        if (result && offsetSeconds > 0 && chapters.Count > 0)
                        {
                            await ApplyImageCaptureOffsetAsync(taskItem, directoryService, chapters, offsetSeconds,
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info($"VideoThumbnailExtract - Item cancelled: {taskItem.Name} - {taskItem.Path}");
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"VideoThumbnailExtract - Item failed: {taskItem.Name} - {taskItem.Path}");
                        _logger.Error(e.Message);
                        _logger.Debug(e.StackTrace);
                    }
                    finally
                    {
                        if (result && cooldownSeconds.HasValue)
                        {
                            try
                            {
                                await Task.Delay(cooldownSeconds.Value * 1000, cancellationToken).ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        QueueManager.MasterSemaphore.Release();

                        var currentCount = Interlocked.Increment(ref current);
                        progress.Report(currentCount / total * 100);

                        if (!mediaInfoRestoreMode)
                        {
                            _logger.Info(
                                $"VideoThumbnailExtract - Progress {currentCount}/{total} - Task {taskIndex}: {taskItem.Path}");
                        }
                    }
                }, cancellationToken);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (items.Count > 0) IsRunning = false;

            progress.Report(100.0);
            _logger.Info($"VideoThumbnailExtract - Number of items skipped: {skip}");
            _logger.Info("VideoThumbnailExtract - Scheduled Task Complete");
        }

        /// <summary>
        /// Pro: applies the ImageCaptureOffsetSeconds offset to chapter image capture
        /// positions. Emby's ThumbnailGenerator only extracts chapter images (no
        /// percentage-based capture exists in this code path), so the offset shifts each
        /// chapter's capture point forward by the configured seconds, clamped to the item's
        /// runtime. Extraction runs on a throw-away copy of the chapters (saveChapters:
        /// false), then the produced image paths are copied back onto the real chapters and
        /// persisted.
        /// </summary>
        private async Task ApplyImageCaptureOffsetAsync(Video taskItem, IDirectoryService directoryService,
            List<ChapterInfo> chapters, int offsetSeconds, CancellationToken cancellationToken)
        {
            var runTimeTicks = taskItem.RunTimeTicks ?? 0;
            if (runTimeTicks <= 0) return;

            var offsetTicks = TimeSpan.FromSeconds(offsetSeconds).Ticks;
            var offsetChapters = new List<ChapterInfo>(chapters.Count);
            var changedIndexes = new List<int>(chapters.Count);

            for (var i = 0; i < chapters.Count; i++)
            {
                var source = chapters[i];
                var newPosition = source.StartPositionTicks + offsetTicks;
                if (newPosition > runTimeTicks) newPosition = runTimeTicks;
                if (newPosition < 0) newPosition = 0;
                if (newPosition == source.StartPositionTicks) continue;

                offsetChapters.Add(new ChapterInfo
                {
                    Name = source.Name,
                    StartPositionTicks = newPosition,
                    ImagePath = source.ImagePath,
                    ImageTag = source.ImageTag,
                    ImageDateModified = source.ImageDateModified
                });
                changedIndexes.Add(i);
            }

            if (offsetChapters.Count == 0) return;

            var libraryOptions = _libraryManager.GetLibraryOptions(taskItem);
            var extracted = await Plugin.VideoThumbnailApi
                .RefreshThumbnailImages(taskItem, libraryOptions, directoryService, offsetChapters, true, false,
                    cancellationToken).ConfigureAwait(false);

            if (!extracted) return;

            var anyChanged = false;
            for (var i = 0; i < offsetChapters.Count; i++)
            {
                var offsetChapter = offsetChapters[i];
                if (string.IsNullOrEmpty(offsetChapter.ImagePath)) continue;
                var target = chapters[changedIndexes[i]];
                if (target.ImagePath == offsetChapter.ImagePath) continue;
                target.ImagePath = offsetChapter.ImagePath;
                target.ImageTag = offsetChapter.ImageTag;
                target.ImageDateModified = offsetChapter.ImageDateModified;
                anyChanged = true;
            }

            if (anyChanged)
            {
                _itemRepository.SaveChapters(taskItem.InternalId, true, chapters);
                _logger.Info($"VideoThumbnailExtract - Applied image capture offset {offsetSeconds}s to " +
                             $"{offsetChapters.Count} chapter image(s): {taskItem.Name}");
            }
        }

        public string Category =>
            Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
                Plugin.Instance.DefaultUICulture);

        public string Key => "VideoThumbnailExtractTask";

        public string Description => Resources.ResourceManager.GetString(
            "ExtractVideoThumbnailTask_Description_Extract_video_thumbnail_preview",
            Plugin.Instance.DefaultUICulture);

        public string Name => "Extract Video Thumbnail";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        public static bool IsRunning { get; private set; }
    }
}

using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    public class ScanExternalTrackTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;

        public ScanExternalTrackTask(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
            _logger = Plugin.Instance.Logger;
        }

        public string Key => "ScanExternalTrackTask";

        public string Name => "Scan External Tracks";

        public string Description => Resources.ResourceManager.GetString(
            "ScanExternalTrackTask_Description_Scans_libraries_for_external_audio_track_files",
            Plugin.Instance.DefaultUICulture);

        public string Category => Resources.ResourceManager.GetString(
            "PluginOptions_EditorTitle_Strm_Assistant", Plugin.Instance.DefaultUICulture);

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("ExternalTrack - Scheduled Task Execute");
            await Task.Yield();
            progress.Report(0);

            var options = Plugin.Instance.GetPluginOptions().ExperienceEnhanceOptions;
            if (options == null || !options.EnableExternalTrackScan)
            {
                _logger.Info("ExternalTrack - External track scan is disabled; skipped");
                progress.Report(100);
                return;
            }

            var formats = (options.ExternalTrackFormats ?? "mka,ac3,eac3")
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.TrimStart('.').ToLowerInvariant())
                .Where(f => f.Length > 0)
                .ToHashSet();

            if (formats.Count == 0)
            {
                _logger.Info("ExternalTrack - No external track formats configured; skipped");
                progress.Report(100);
                return;
            }

            var videoByBasename = new Dictionary<string, List<BaseItem>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var query = new InternalItemsQuery
                {
                    HasPath = true,
                    MediaTypes = new[] { MediaType.Video }
                };
                foreach (var video in _libraryManager.GetItemList(query))
                {
                    if (string.IsNullOrWhiteSpace(video.Path)) continue;
                    var key = Path.GetFileNameWithoutExtension(video.Path);
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!videoByBasename.TryGetValue(key, out var list))
                        videoByBasename[key] = list = new List<BaseItem>();
                    list.Add(video);
                }
            }
            catch (Exception e)
            {
                _logger.Error("ExternalTrack - Failed to query library videos: " + e.Message);
                progress.Report(100);
                return;
            }

            System.Collections.Generic.List<MediaBrowser.Model.Entities.VirtualFolderInfo> virtualFolders;
            try { virtualFolders = _libraryManager.GetVirtualFolders(); }
            catch (Exception e)
            {
                _logger.Error("ExternalTrack - Failed to get virtual folders: " + e.Message);
                progress.Report(100);
                return;
            }

            var scannedLibraries = 0;
            var totalTracks = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in virtualFolders)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var locations = folder.Locations ?? Array.Empty<string>();
                if (locations.Length == 0) continue;
                scannedLibraries++;

                foreach (var location in locations)
                {
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(location, "*", SearchOption.AllDirectories); }
                    catch (Exception e)
                    {
                        _logger.Warn("ExternalTrack - Failed to enumerate: " + location + " (" + e.Message + ")");
                        continue;
                    }

                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (!formats.Contains(Path.GetExtension(file)?.TrimStart('.').ToLowerInvariant() ?? string.Empty))
                            continue;
                        if (!seen.Add(file)) continue;

                        var baseName = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrEmpty(baseName) || !videoByBasename.TryGetValue(baseName, out var videos) || videos.Count == 0)
                            continue;

                        foreach (var video in videos)
                        {
                            if (!string.Equals(Path.GetDirectoryName(video.Path), Path.GetDirectoryName(file), StringComparison.OrdinalIgnoreCase))
                                continue;
                            totalTracks++;
                            _logger.Info("ExternalTrack - Found: " + file + " for " + video.Name);
                        }
                    }
                }
            }

            _logger.Info("ExternalTrack - Scan complete: " + totalTracks + " tracks found in " + scannedLibraries + " libraries");
            progress.Report(100);
        }

        public System.Collections.Generic.IEnumerable<TaskTriggerInfo> GetDefaultTriggers() { return Array.Empty<TaskTriggerInfo>(); }

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;
    }
}

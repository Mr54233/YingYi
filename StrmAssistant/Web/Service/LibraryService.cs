using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static StrmAssistant.Common.CommonUtility;
using static StrmAssistant.Common.LanguageUtility;

namespace StrmAssistant.Web.Service
{
    public class LibraryService : BaseApiService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;
        private readonly IItemRepository _itemRepository;

        public LibraryService(ILibraryManager libraryManager, IItemRepository itemRepository, IFileSystem fileSystem)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
        }

        public void Any(DeleteVersion request)
        {
            var item = _libraryManager.GetItemById(request.Id);

            if (!(item is Video video) || !(video is Movie || video is Episode) || !video.IsFileProtocol ||
                video.GetAlternateVersionIds().Count == 0)
            {
                return;
            }
            
            var user = GetUserForRequest(null);
            var collectionFolders = _libraryManager.GetCollectionFolders(item);

            if (user is null)
            {
                if (!item.CanDelete())
                {
                    return;
                }
            }
            else if (!item.CanDelete(user, collectionFolders))
            {
                return;
            }

            var deleteItems = item is Episode episode && request.DeleteParent
                ? GetSeasonEpisodesSameVersion(episode)
                : new List<BaseItem> { item };

            var enableDeepDelete = Plugin.Instance.GetPluginOptions().ExperienceEnhanceOptions.EnableDeepDelete;
            _logger.Info("DeleteVersion - Deep delete is " + (enableDeepDelete ? "enabled" : "disabled") +
                         "; physical files will " + (enableDeepDelete ? "be deleted" : "be kept"));

            foreach (var deleteItem in deleteItems)
            {
                var proceedToDelete = true;
                var deletePaths = enableDeepDelete
                    ? Plugin.LibraryApi.GetDeletePaths(deleteItem)
                    : Array.Empty<MediaBrowser.Model.IO.FileSystemMetadata>();

                foreach (var path in deletePaths)
                {
                    try
                    {
                        if (!path.IsDirectory)
                        {
                            _logger.Info("DeleteVersion - Attempting to delete file: " + path.FullName);
                            _fileSystem.DeleteFile(path.FullName, true);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is IOException || e is UnauthorizedAccessException)
                        {
                            proceedToDelete = false;
                            _logger.Error("DeleteVersion - Failed to delete file: " + path.FullName);
                            _logger.Error(e.Message);
                            _logger.Debug(e.StackTrace);
                        }
                    }
                }

                if (proceedToDelete)
                {
                    _itemRepository.DeleteItems(new[] { deleteItem });

                    try
                    {
                        _fileSystem.DeleteDirectory(item.GetInternalMetadataPath(), true, true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        public void Any(ClearChapterImage request)
        {
            var item = _libraryManager.GetItemById(request.Id);

            if (!(item is Video) || !item.IsFileProtocol)
            {
                return;
            }

            List<MediaBrowser.Model.Entities.ChapterInfo> chapters;
            try { chapters = _itemRepository.GetChapters(item); }
            catch (Exception e)
            {
                _logger.Error("ClearChapterImage - Failed to read chapters for " + item.Name + ": " + e.Message);
                return;
            }

            if (chapters == null || chapters.Count == 0)
            {
                _logger.Info("ClearChapterImage - No chapters found for: " + item.Name);
                return;
            }

            var cleared = 0;
            foreach (var chapter in chapters)
            {
                if (string.IsNullOrEmpty(chapter.ImagePath)) continue;
                try
                {
                    _logger.Info("ClearChapterImage - Deleting: " + chapter.ImagePath);
                    _fileSystem.DeleteFile(chapter.ImagePath, true);
                }
                catch (Exception e)
                {
                    _logger.Error("ClearChapterImage - Failed to delete: " + chapter.ImagePath);
                    _logger.Error(e.Message);
                }
                chapter.ImagePath = null;
                chapter.ImageTag = null;
                cleared++;
            }

            if (cleared == 0)
            {
                _logger.Info("ClearChapterImage - Nothing to clear for: " + item.Name);
                return;
            }

            try
            {
                _itemRepository.SaveChapters(item.InternalId, chapters);
                _logger.Info("ClearChapterImage - Cleared " + cleared + " chapter image(s) for: " + item.Name);
            }
            catch (Exception e)
            {
                _logger.Error("ClearChapterImage - Failed to save chapters for " + item.Name + ": " + e.Message);
            }
        }

        private List<BaseItem> GetSeasonEpisodesSameVersion(Episode episode)
        {
            var seasonFolderChildren = episode.Parent.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { nameof(Episode) },
                    Recursive = false,
                    GroupByPresentationUniqueKey = false,
                })
                .ToList();

            var seasonEpisodesCount = episode.Season.GetEpisodeIds(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { nameof(Episode) }, GroupByPresentationUniqueKey = true
                })
                .Length;

            if (seasonFolderChildren.Count == seasonEpisodesCount)
            {
                return seasonFolderChildren;
            }

            var targetCleaned = CleanEpisodeName(episode.FileNameWithoutExtension);

            var allEpisodes = episode.Season
                .GetEpisodes(new InternalItemsQuery
                {
                    GroupByPresentationUniqueKey = false, EnableTotalRecordCount = false
                })
                .Items.ToList();

            var similarEpisodes = new List<BaseItem>();

            foreach (var ep in allEpisodes)
            {
                var cleanedName = CleanEpisodeName(ep.FileNameWithoutExtension);
                var similarity = LevenshteinDistance(targetCleaned, cleanedName);

                if (similarity > 0.92)
                {
                    similarEpisodes.Add(ep);
                }
            }

            return similarEpisodes;
        }
    }
}

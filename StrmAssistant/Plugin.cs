using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Common;
using StrmAssistant.IntroSkip;
using StrmAssistant.Options;
using StrmAssistant.Properties;
using StrmAssistant.ScheduledTask;
using StrmAssistant.Web.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Options.GeneralOptions;
using static StrmAssistant.Options.IntroSkipOptions;
using static StrmAssistant.Options.MediaInfoExtractOptions;
using static StrmAssistant.Options.MetadataEnhanceOptions;
using static StrmAssistant.Options.Utility;

namespace StrmAssistant
{
    public class Plugin: BasePluginSimpleUI<PluginOptions>, IHasUIPages, IHasThumbImage
    {
        private List<MediaBrowser.Model.Plugins.UI.IPluginUIPageController> _tabPages;
        private readonly object _tabPagesLock = new object();

        public IReadOnlyCollection<MediaBrowser.Model.Plugins.UI.IPluginUIPageController> UIPageControllers
        {
            get
            {
                // 宿主可能在多请求线程上首次读取；懒加载需加锁避免重复构造/注册。
                if (_tabPages == null)
                {
                    lock (_tabPagesLock)
                    {
                        if (_tabPages == null)
                        {
                            PluginInfo basePluginInfo = base.GetPluginInfo();
                            _tabPages = new List<MediaBrowser.Model.Plugins.UI.IPluginUIPageController>
                            {
                                new Web.Helper.SettingsPageController(this, basePluginInfo, Logger,
                                    GetPluginOptions, SaveOptions, options => PopulateUIOptions(options))
                            };
                        }
                    }
                }
                return _tabPages.AsReadOnly();
            }
        }

        public static Plugin Instance { get; private set; }
        public static LibraryApi LibraryApi { get; private set; }
        public static MediaInfoApi MediaInfoApi { get; private set; }
        public static ChapterApi ChapterApi { get; private set; }
        public static FingerprintApi FingerprintApi { get; private set; }
        public static NotificationApi NotificationApi { get; private set; }
        public static SubtitleApi SubtitleApi { get; private set; }
        public static PlaySessionMonitor PlaySessionMonitor { get; private set; }
        public static MetadataApi MetadataApi { get; private set; }
        public static VideoThumbnailApi VideoThumbnailApi { get; private set; }

        private readonly Guid _id = new Guid("c2f53351-7269-4c07-8a41-8310650a9cbc");

        public readonly ILogger Logger;
        public readonly IApplicationHost ApplicationHost;
        public readonly IApplicationPaths ApplicationPaths;

        /// <summary>
        ///     Exposed so <see cref="Pinyin.EmbyDatabase"/> can reflect the ConnectionManager
        ///     out of it (plan §2.5). Kept public to stay reachable from the module.
        /// </summary>
        public readonly IItemRepository ItemRepository;

        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ITaskManager _taskManager;
        private readonly IFfmpegManager _ffmpegManager;

        private bool _currentSuppressOnOptionsSaved;
        private int _currentMaxConcurrentCount;
        private int _currentTier2ConcurrentCount;
        private bool _currentPersistMediaInfo;
        private bool _currentMediaInfoRestoreMode;
        private bool _currentCatchupMode;
        private bool _currentEnableIntroSkip;
        private bool _currentUnlockIntroSkip;
        private bool _currentEnableAutomaticIntroScraping;
        private bool _currentMergeMultiVersion;
        private bool _currentPinyinSearch;
        private bool _currentPinyinSortName;

        private Pinyin.EmbyDatabase _pinyinDatabase;
        private Pinyin.PinyinScanScheduler _pinyinScheduler;

        /// <summary>
        ///     P1-1 / P1-2 database access. Null only before the constructor ran.
        /// </summary>
        internal Pinyin.EmbyDatabase PinyinDatabase => _pinyinDatabase;

        /// <summary>
        ///     P1-1 / P1-2 timing layer (catch-up, post scan, event queue).
        /// </summary>
        internal Pinyin.PinyinScanScheduler PinyinScheduler => _pinyinScheduler;

        /// <summary>
        ///     Absolute path of the injection cursor, so the manual task can reuse the
        ///     same progress marker as the background workers.
        /// </summary>
        public string PinyinCursorFilePath => _pinyinCursorFilePath;

        private string _pinyinCursorFilePath;

        private static readonly HashSet<string> ExcludedCollectionTypes = new HashSet<string>
        {
            CollectionType.Books.ToString(),
            CollectionType.Photos.ToString(),
            CollectionType.Games.ToString(),
            CollectionType.LiveTv.ToString(),
            CollectionType.Playlists.ToString(),
            CollectionType.BoxSets.ToString()
        };

        public Plugin(IApplicationHost applicationHost, IApplicationPaths applicationPaths, ILogManager logManager,
            IFileSystem fileSystem, ILibraryManager libraryManager, ISessionManager sessionManager,
            IItemRepository itemRepository, INotificationManager notificationManager, ILibraryMonitor libraryMonitor,
            IMediaSourceManager mediaSourceManager, IMediaMountManager mediaMountManager,
            IProviderManager providerManager, IMediaProbeManager mediaProbeManager,
            ILocalizationManager localizationManager, IUserManager userManager, IUserDataManager userDataManager,
            IFfmpegManager ffmpegManager, IMediaEncoder mediaEncoder, IJsonSerializer jsonSerializer,
            IHttpClient httpClient, IServerApplicationHost serverApplicationHost,
            IServerConfigurationManager configurationManager, ITaskManager taskManager,
            IImageExtractionManager imageExtractionManager, IServerApplicationPaths serverApplicationPaths) : base(applicationHost)
        {
            Instance = this;
            Logger = logManager.GetLogger(Name);
            Logger.Info("Plugin is getting loaded.");
            ApplicationHost = applicationHost;
            ApplicationPaths = applicationPaths;

            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _taskManager = taskManager;
            _ffmpegManager = ffmpegManager;
            ItemRepository = itemRepository;

            _currentMaxConcurrentCount = GetOptions().GeneralOptions.MaxConcurrentCount;
            _currentPersistMediaInfo = GetOptions().MediaInfoExtractOptions.PersistMediaInfoMode !=
                                       PersistMediaInfoOption.None.ToString();
            _currentMediaInfoRestoreMode = GetOptions().MediaInfoExtractOptions.PersistMediaInfoMode ==
                                           PersistMediaInfoOption.Restore.ToString();
            _currentCatchupMode = GetOptions().GeneralOptions.CatchupMode;
            _currentEnableIntroSkip = GetOptions().IntroSkipOptions.EnableIntroSkip;
            _currentUnlockIntroSkip = GetOptions().IntroSkipOptions.UnlockIntroSkip;
            _currentEnableAutomaticIntroScraping = GetOptions().IntroSkipOptions.EnableAutomaticIntroScraping;
            Utility.AutoIntroScrapeActive = _currentEnableAutomaticIntroScraping;
            _currentMergeMultiVersion = GetOptions().ExperienceEnhanceOptions.MergeMultiVersion;
            _currentPinyinSearch = GetOptions().PinyinEnhanceOptions.EnablePinyinSearch;
            _currentPinyinSortName = GetOptions().PinyinEnhanceOptions.EnablePinyinSortName;
            InitializeOptionCache();

            if (GetOptions().AboutOptions.DebugMode)
            {
                DebugMode = true;
                GetOptions().AboutOptions.DebugMode = false;
                SavePluginOptionsSuppress();
            }
            else if (Debugger.IsAttached)
            {
                DebugMode = true;
            }

            LibraryApi = new LibraryApi(libraryManager, providerManager, fileSystem, mediaMountManager, userManager);
            MediaInfoApi = new MediaInfoApi(libraryManager, fileSystem, providerManager, mediaSourceManager,
                itemRepository, jsonSerializer, libraryMonitor);
            ChapterApi = new ChapterApi(libraryManager, itemRepository);
            FingerprintApi = new FingerprintApi(libraryManager, fileSystem, applicationPaths, ffmpegManager,
                mediaEncoder, mediaMountManager, jsonSerializer, itemRepository, serverApplicationHost);
            PlaySessionMonitor = new PlaySessionMonitor(libraryManager, userManager, sessionManager);
            NotificationApi = new NotificationApi(notificationManager, userManager, sessionManager);
            SubtitleApi = new SubtitleApi(libraryManager, fileSystem, mediaProbeManager, localizationManager,
                itemRepository);
            MetadataApi = new MetadataApi(libraryManager, fileSystem, configurationManager, localizationManager,
                jsonSerializer, httpClient);
            VideoThumbnailApi = new VideoThumbnailApi(libraryManager, fileSystem, imageExtractionManager, itemRepository,
                mediaMountManager, serverApplicationPaths, libraryMonitor, ffmpegManager);
            ShortcutMenuHelper.Initialize(configurationManager);

            if (_currentCatchupMode || _currentEnableAutomaticIntroScraping) QueueManager.Initialize();
            if (_currentEnableIntroSkip) PlaySessionMonitor.Initialize();

            // P1-1 / P1-2 wiring. Constructing the layer does no database work and no
            // blocking wait, so it is safe inside the constructor; the catch-up run is
            // scheduled by Start() as a detached Task.Run with a 60s Thread.Sleep.
            _pinyinDatabase = new Pinyin.EmbyDatabase(Logger);
            _pinyinCursorFilePath = Path.Combine(ApplicationPaths.ProgramDataPath, "plugins", "影医",
                Pinyin.PinyinScanScheduler.CursorFileName);
            _pinyinScheduler = new Pinyin.PinyinScanScheduler(Logger, _pinyinDatabase, _libraryManager,
                ApplicationPaths.ProgramDataPath);
            if (_currentPinyinSearch || _currentPinyinSortName) _pinyinScheduler.Start();

            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemUpdated += OnItemUpdated;
            _libraryManager.ItemRemoved += OnItemRemoved;
            _providerManager.RefreshCompleted += OnRefreshCompleted;
            _userManager.UserCreated += OnUserCreated;
            _userManager.UserDeleted += OnUserDeleted;
            _userManager.UserConfigurationUpdated += OnUserConfigurationUpdated;
            _userDataManager.UserDataSaved += OnUserDataSaved;
            CollectionFolder.LibraryOptionsUpdated += OnLibraryOptionsUpdated;
        }

        private void OnRefreshCompleted(object sender, GenericEventArgs<RefreshProgressInfo> e)
        {
            // A metadata refresh resets SortName and rewrites the FTS document, so the
            // pinyin layer must re-apply before the early return below.
            if (_currentPinyinSearch || _currentPinyinSortName)
            {
                try
                {
                    _pinyinScheduler?.Enqueue(e.Argument.Item.InternalId);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex.Message);
                }
            }

            if (_libraryManager.IsScanRunning) return;

            if (_currentMergeMultiVersion && e.Argument.Item.IsTopParent)
            {
                var library = e.Argument.CollectionFolders.OfType<CollectionFolder>().FirstOrDefault();

                if (library != null && (library.CollectionType == CollectionType.Movies.ToString() ||
                                        library.CollectionType is null))
                {
                    MergeMultiVersionTask.CurrentScanLibrary.Value = library;

                    var mergeMoviesTask = _taskManager.ScheduledTasks.FirstOrDefault(t =>
                        t.ScheduledTask is MergeMultiVersionTask);

                    if (mergeMoviesTask != null)
                    {
                        _ = _taskManager.Execute(mergeMoviesTask, new TaskOptions());
                    }
                }
            }
        }

        private void OnUserCreated(object sender, GenericEventArgs<User> e)
        {
            LibraryApi.FetchUsers();
        }

        private void OnUserDeleted(object sender, GenericEventArgs<User> e)
        {
            LibraryApi.FetchUsers();
        }
        
        private void OnUserConfigurationUpdated(object sender, GenericEventArgs<User> e)
        {
            if (e.Argument.Policy.IsAdministrator) LibraryApi.FetchAdminOrderedViews();
        }

        private void OnLibraryOptionsUpdated(object sender, GenericEventArgs<Tuple<CollectionFolder, LibraryOptions>> e)
        {
            var library = e.Argument.Item1;

            if (!LibraryApi.ExcludedCollectionTypes.Contains(library.CollectionType))
            {
                LibraryApi.UpdateLibraryPathsInScope();

                if (library.CollectionType == CollectionType.TvShows.ToString() || library.CollectionType is null)
                {
                    PlaySessionMonitor.UpdateLibraryPathsInScope();
                    FingerprintApi.UpdateLibraryPathsInScope();

                    if (_currentUnlockIntroSkip)
                        FingerprintApi.UpdateLibraryIntroDetectionFingerprintLength();

                    if (_currentMergeMultiVersion)
                        LibraryApi.EnsureLibraryEnabledAutomaticSeriesGrouping();
                }
            }
        }

        private async void OnItemAdded(object sender, ItemChangeEventArgs e)
        {
            try
            {
                var deserializeResult = false;

                if (_currentPersistMediaInfo && e.Item is Video)
                {
                    deserializeResult = LibraryApi.HasMediaInfo(e.Item);

                    var directoryService = new DirectoryService(Logger, _fileSystem);

                    if (!deserializeResult)
                    {
                        deserializeResult = await MediaInfoApi.DeserializeMediaInfo(e.Item, directoryService,
                            "OnItemAdded Restore", true).ConfigureAwait(false);
                    }
                    else
                    {
                        _ = MediaInfoApi.SerializeMediaInfo(e.Item.InternalId, directoryService, true,
                            "OnItemAdded Overwrite").ConfigureAwait(false);
                    }
                }

                // Pro: IntroSkipScrapeOptions.EnableAutomaticIntroScraping - queue new episodes
                // for fingerprint extraction on ingest (no catchup-mode requirement).
                if (_currentEnableAutomaticIntroScraping && e.Item is Episode newEpisode &&
                    FingerprintApi.IsLibraryInScope(newEpisode))
                {
                    QueueManager.FingerprintItemQueue.Enqueue(newEpisode);
                }

                if (_currentCatchupMode && (e.Item is Video || e.Item is Audio))
                {
                    if (_currentUnlockIntroSkip && IsCatchupTaskSelected(CatchupTask.Fingerprint) &&
                        e.Item is Episode && FingerprintApi.IsLibraryInScope(e.Item) &&
                        (!deserializeResult || FingerprintApi.IsExtractNeeded(e.Item)))
                    {
                        QueueManager.FingerprintItemQueue.Enqueue(e.Item);
                    }
                    else
                    {
                        if (IsCatchupTaskSelected(CatchupTask.MediaInfo) && !deserializeResult)
                        {
                            QueueManager.MediaInfoExtractItemQueue.Enqueue(e.Item);
                        }

                        if (IsCatchupTaskSelected(CatchupTask.IntroSkip) &&
                            e.Item is Episode && PlaySessionMonitor.IsLibraryInScope(e.Item))
                        {
                            if (!deserializeResult)
                            {
                                QueueManager.MediaInfoExtractItemQueue.Enqueue(e.Item);
                            }
                            else if (e.Item is Episode episode && ChapterApi.SeasonHasIntroCredits(episode))
                            {
                                QueueManager.IntroSkipItemQueue.Enqueue(episode);
                            }
                        }
                    }

                    if (IsCatchupTaskSelected(CatchupTask.EpisodeRefresh) && e.Item is Episode ep &&
                        LibraryApi.IsPremiereDateInScope(ep, DateTimeOffset.UtcNow.AddDays(-90), false))
                    {
                        QueueManager.EpisodeRefreshItemQueue.Enqueue(ep);
                    }
                }

                if (e.Item is Movie || e.Item is Series || e.Item is Episode)
                {
                    NotificationApi.FavoritesUpdateSendNotification(e.Item);
                }

                // P1-1 / P1-2 third timing layer: queue the item and let the 30s timer
                // batch write it. Never touch the database from this handler.
                if (_currentPinyinSearch || _currentPinyinSortName)
                {
                    _pinyinScheduler?.Enqueue(e.Item.InternalId);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex.Message);
                Logger.Debug(ex.StackTrace);
            }
        }

        private void OnItemUpdated(object sender, ItemChangeEventArgs e)
        {
            try
            {
                if (!_currentPinyinSearch && !_currentPinyinSortName) return;

                // Emby rewrites the FTS document with the plain name and resets SortName
                // on every metadata update, so both have to be re-applied here.
                _pinyinScheduler?.Enqueue(e.Item.InternalId);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex.Message);
                Logger.Debug(ex.StackTrace);
            }
        }

        private void OnItemRemoved(object sender, ItemChangeEventArgs e)
        {
            if (!_currentMediaInfoRestoreMode && e.Item is Video)
            {
                var directoryService = new DirectoryService(Logger, _fileSystem);
                MediaInfoApi.DeleteMediaInfoJson(e.Item, directoryService, "Item Removed Event");
            }
        }

        private void OnUserDataSaved(object sender, UserDataSaveEventArgs e)
        {
            if (e.UserData.IsFavorite)
            {
                if (_currentUnlockIntroSkip && _currentCatchupMode && IsCatchupTaskSelected(CatchupTask.Fingerprint) &&
                    FingerprintApi.IsLibraryInScope(e.Item) && (e.Item is Episode || e.Item is Series))
                {
                    QueueManager.FingerprintItemQueue.Enqueue(e.Item);
                }
                else if (_currentCatchupMode && IsCatchupTaskSelected(CatchupTask.MediaInfo))
                {
                    QueueManager.MediaInfoExtractItemQueue.Enqueue(e.Item);
                }
            }
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public override string Description => "影医 — STRM 媒体库诊疗与增强";

        public override Guid Id => _id;

        public sealed override string Name => "影医";

        public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        public string UserAgent => $"YingYi/{CurrentVersion}";

        public CultureInfo DefaultUICulture => new CultureInfo("zh-CN");

        public bool DebugMode;

        public bool IsModSupported => false;

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Properties.thumb.png");
        }

        public PluginOptions GetPluginOptions()
        {
            return GetOptions();
        }

        /// <summary>
        ///     分 Tab 设置页的 internal 保存桥：读全量选项 → 用 Tab 提交上来的 section 替换
        ///     对应属性 → 走 base.SaveOptions 触发 OnOptionsSaving/OnOptionsSaved 事件链
        ///     （运行时开关同步、拼音调度器起停、ffmpeg 路径应用等全部保持原行为）。
        /// </summary>
        internal void ApplySection(IEditableObject section, Action<PluginOptions> populateUI)
        {
            var options = GetPluginOptions();
            // Clone section values were deserialized by GenericUI; keep the full root object
            // and only replace the matching section below. The base SaveOptions path remains
            // the single writer and executes all existing cross-section validation/hooks.

            // ContentData 是 GenericUI 反序列化后的新 section 实例，不能用引用相等判断。
            // 必须按真实类型写回全量 PluginOptions，否则 tab 保存会 200 但值不落盘。
            switch (section)
            {
                case GeneralOptions value:
                    options.GeneralOptions = value;
                    break;
                case MediaInfoExtractOptions value:
                    options.MediaInfoExtractOptions = value;
                    break;
                case PinyinEnhanceOptions value:
                    options.PinyinEnhanceOptions = value;
                    break;
                case MetadataEnhanceOptions value:
                    options.MetadataEnhanceOptions = value;
                    break;
                case StrmAssistant.Options.MetadataRefreshOptions value:
                    options.MetadataRefreshOptions = value;
                    break;
                case MetadataBuildOptions value:
                    options.MetadataBuildOptions = value;
                    break;
                case MetadataScrapeOptions value:
                    options.MetadataScrapeOptions = value;
                    break;
                case IntroSkipOptions value:
                    options.IntroSkipOptions = value;
                    break;
                case ExperienceEnhanceOptions value:
                    options.ExperienceEnhanceOptions = value;
                    break;
                case UIFunctionOptions value:
                    options.UIFunctionOptions = value;
                    break;
                case ServerOptions value:
                    options.ServerOptions = value;
                    break;
                case AboutOptions value:
                    options.AboutOptions = value;
                    break;
                default:
                    throw new ArgumentException("Unknown settings tab section: " + section?.GetType().FullName, nameof(section));
            }

            populateUI(options);

            // 跨节校验：CatchupMode+Fingerprint 需 UnlockIntroSkip（原 PluginOptions.Validate）。
            // 单页 UI 由 ContentData=根对象时 Validate 覆盖；Tab 化后根级 Validate 不再自动执行，
            // 这里复刻同一条规则并拒绝保存（与旧 UI 弹错一致，而不是静默剥离用户勾选）。
            if (options.GeneralOptions.CatchupMode &&
                options.GeneralOptions.CatchupTaskScope.Contains(CatchupTask.Fingerprint.ToString()) &&
                !options.IntroSkipOptions.UnlockIntroSkip)
            {
                throw new ArgumentException(Resources.InvalidFingerprintCatchup, nameof(section));
            }

            SaveOptions(options);
        }

        public void SavePluginOptionsSuppress()
        {
            _currentSuppressOnOptionsSaved = true;
            SaveOptions(GetOptions());
        }

        protected override bool OnOptionsSaving(PluginOptions options)
        {
            if (string.IsNullOrEmpty(options.GeneralOptions.CatchupTaskScope))
            {
                options.GeneralOptions.CatchupTaskScope = CatchupTask.MediaInfo.ToString();
            }
            else if (!options.IntroSkipOptions.UnlockIntroSkip)
            {
                var taskScope = options.GeneralOptions.CatchupTaskScope;
                var selectedTasks = taskScope.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(f => f != CatchupTask.Fingerprint.ToString())
                    .ToList();
                options.GeneralOptions.CatchupTaskScope = string.Join(",", selectedTasks);
            }

            options.MediaInfoExtractOptions.LibraryScope = string.Join(",",
                options.MediaInfoExtractOptions.LibraryScope
                    ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(v => options.MediaInfoExtractOptions.LibraryList.Any(option => option.Value == v)) ??
                Enumerable.Empty<string>());

            options.IntroSkipOptions.LibraryScope = string.Join(",",
                options.IntroSkipOptions.LibraryScope
                    ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(v => options.IntroSkipOptions.LibraryList.Any(option => option.Value == v)) ??
                Enumerable.Empty<string>());

            options.IntroSkipOptions.UserScope = string.Join(",",
                options.IntroSkipOptions.UserScope
                    ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(v => options.IntroSkipOptions.UserList.Any(option => option.Value == v)) ??
                Enumerable.Empty<string>());

            options.IntroSkipOptions.MarkerEnabledLibraryScope =
                options.IntroSkipOptions.MarkerEnabledLibraryScope
                    ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Contains("-1") == true
                    ? "-1"
                    : string.Join(",",
                        options.IntroSkipOptions.MarkerEnabledLibraryScope
                            ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(v => options.IntroSkipOptions.MarkerEnabledLibraryList.Any(option =>
                                option.Value == v)) ?? Enumerable.Empty<string>());

            var pinyinLibraryList = options.PinyinEnhanceOptions.LibraryList ??
                                    Enumerable.Empty<EditorSelectOption>();
            options.PinyinEnhanceOptions.LibraryScope = string.Join(",",
                options.PinyinEnhanceOptions.LibraryScope
                    ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(v => pinyinLibraryList.Any(option => option.Value == v)) ??
                Enumerable.Empty<string>());

            return base.OnOptionsSaving(options);
        }

        protected override void OnOptionsSaved(PluginOptions options)
        {
            var suppress = _currentSuppressOnOptionsSaved;

            if (_currentCatchupMode != options.GeneralOptions.CatchupMode)
            {
                _currentCatchupMode = options.GeneralOptions.CatchupMode;
                if (options.GeneralOptions.CatchupMode)
                {
                    QueueManager.Initialize();
                }
                else if (!_currentEnableAutomaticIntroScraping)
                {
                    // keep the queue monitor alive when automatic intro scraping needs it
                    QueueManager.Dispose();
                }
            }
            else if (!options.GeneralOptions.CatchupMode &&
                     _currentEnableAutomaticIntroScraping != options.IntroSkipOptions.EnableAutomaticIntroScraping)
            {
                // catchup stayed off but the auto-scrape flag flipped - make sure the
                // fingerprint queue monitor reflects the new state.
                _currentEnableAutomaticIntroScraping = options.IntroSkipOptions.EnableAutomaticIntroScraping;
                Utility.AutoIntroScrapeActive = _currentEnableAutomaticIntroScraping;
                if (_currentEnableAutomaticIntroScraping) QueueManager.Initialize();
            }
            UpdateCatchupScope();

            if (!suppress)
            {
                Logger.Info("CatchupMode is set to {0}", options.GeneralOptions.CatchupMode);
                var catchupTaskScope = GetSelectedCatchupTaskDescription();
                Logger.Info("CatchupTaskScope is set to {0}", string.IsNullOrEmpty(catchupTaskScope) ? "EMPTY" : catchupTaskScope);
            }

            if (!suppress)
            {
                Logger.Info("IncludeExtra is set to {0}", options.MediaInfoExtractOptions.IncludeExtra);
                Logger.Info("MaxConcurrentCount is set to {0}", options.GeneralOptions.MaxConcurrentCount);
                Logger.Info("CooldownDurationSeconds is set to {0}", options.GeneralOptions.CooldownDurationSeconds);
                Logger.Info("Tier2 MaxConcurrentCount is set to {0}", options.GeneralOptions.Tier2MaxConcurrentCount);

                var libraryScope = string.Join(", ",
                    options.MediaInfoExtractOptions.LibraryScope
                        ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v =>
                            options.MediaInfoExtractOptions.LibraryList.FirstOrDefault(option => option.Value == v)
                                ?.Name) ?? Enumerable.Empty<string>());
                Logger.Info("MediaInfoExtract - LibraryScope is set to {0}",
                    string.IsNullOrEmpty(libraryScope) ? "ALL" : libraryScope);

                Logger.Info("EnableImageCapturePostProcess is set to {0}",
                    options.MediaInfoExtractOptions.EnableImageCapturePostProcess);
                Logger.Info("ImageCaptureOffsetSeconds is set to {0}",
                    options.MediaInfoExtractOptions.ImageCaptureOffsetSeconds);
            }

            ApplyCustomEncoderPath(options.GeneralOptions.CustomEncoderPath, suppress);

            if (_currentMaxConcurrentCount != options.GeneralOptions.MaxConcurrentCount)
            {
                _currentMaxConcurrentCount = options.GeneralOptions.MaxConcurrentCount;

                QueueManager.UpdateMasterSemaphore(_currentMaxConcurrentCount);

                FingerprintApi.PatchTimeout(_currentMaxConcurrentCount);
            }
            if (_currentTier2ConcurrentCount != options.GeneralOptions.Tier2MaxConcurrentCount)
            {
                _currentTier2ConcurrentCount = options.GeneralOptions.Tier2MaxConcurrentCount;

                QueueManager.UpdateTier2Semaphore(_currentTier2ConcurrentCount);
            }
            LibraryApi.UpdateLibraryPathsInScope();

            if (!suppress)
            {
                Logger.Info("PersistMediaInfoMode is set to {0}", options.MediaInfoExtractOptions.PersistMediaInfoMode);
                Logger.Info("MediaInfoJsonRootFolder is set to {0}",
                    !string.IsNullOrEmpty(options.MediaInfoExtractOptions.MediaInfoJsonRootFolder)
                        ? options.MediaInfoExtractOptions.MediaInfoJsonRootFolder
                        : "EMPTY");
            }

            _currentPersistMediaInfo = options.MediaInfoExtractOptions.PersistMediaInfoMode !=
                                       PersistMediaInfoOption.None.ToString();
            _currentMediaInfoRestoreMode = options.MediaInfoExtractOptions.PersistMediaInfoMode ==
                                           PersistMediaInfoOption.Restore.ToString();

            if (!suppress)
            {
                Logger.Info("EnableIntroSkip is set to {0}", options.IntroSkipOptions.EnableIntroSkip);
                Logger.Info("MaxIntroDurationSeconds is set to {0}", options.IntroSkipOptions.MaxIntroDurationSeconds);
                Logger.Info("MaxCreditsDurationSeconds is set to {0}", options.IntroSkipOptions.MaxCreditsDurationSeconds);
                Logger.Info("MinOpeningPlotDurationSeconds is set to {0}", options.IntroSkipOptions.MinOpeningPlotDurationSeconds);
            }
            if (_currentEnableIntroSkip != options.IntroSkipOptions.EnableIntroSkip)
            {
                _currentEnableIntroSkip = options.IntroSkipOptions.EnableIntroSkip;
                if (options.IntroSkipOptions.EnableIntroSkip)
                {
                    PlaySessionMonitor.Initialize();
                }
                else
                {
                    PlaySessionMonitor.Dispose();
                }
            }

            UpdateIntroSkipPreferences();

            if (!suppress)
            {
                var intoSkipLibraryScope = string.Join(", ",
                    options.IntroSkipOptions.LibraryScope?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => options.IntroSkipOptions.LibraryList
                            .FirstOrDefault(option => option.Value == v)
                            ?.Name) ?? Enumerable.Empty<string>());
                Logger.Info("IntroSkip - LibraryScope is set to {0}",
                        string.IsNullOrEmpty(intoSkipLibraryScope) ? "ALL" : intoSkipLibraryScope);

                var introSkipUserScope = string.Join(", ",
                    options.IntroSkipOptions.UserScope?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => options.IntroSkipOptions.UserList
                            .FirstOrDefault(option => option.Value == v)
                            ?.Name) ?? Enumerable.Empty<string>());
                Logger.Info("IntroSkip - UserScope is set to {0}",
                        string.IsNullOrEmpty(introSkipUserScope) ? "ALL" : introSkipUserScope);

                Logger.Info("IntroSkip - ClientScope is set to {0}", options.IntroSkipOptions.ClientScope);
                var introSkipPreferences = GetSelectedIntroSkipPreferenceDescription();

                Logger.Info("IntroSkipPreferences is set to {0}",
                    string.IsNullOrEmpty(introSkipPreferences) ? "EMPTY" : introSkipPreferences);
            }
            PlaySessionMonitor.UpdateLibraryPathsInScope();
            PlaySessionMonitor.UpdateUsersInScope();
            PlaySessionMonitor.UpdateClientInScope();
            
            if (!suppress)
            {
                Logger.Info("UnlockIntroSkip is set to {0}", options.IntroSkipOptions.UnlockIntroSkip);
                var markerEnabledLibraryScope = string.Join(", ",
                    options.IntroSkipOptions.MarkerEnabledLibraryScope
                        ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v =>
                            options.IntroSkipOptions.MarkerEnabledLibraryList
                                .FirstOrDefault(option => option.Value == v)?.Name) ?? Enumerable.Empty<string>());
                Logger.Info("MarkerEnabledLibraryScope is set to {0}",
                    string.IsNullOrEmpty(markerEnabledLibraryScope)
                        ? options.IntroSkipOptions.MarkerEnabledLibraryList.Any(o => o.Value != "-1") ? "ALL" : "EMPTY"
                        : markerEnabledLibraryScope);
                Logger.Info("IntroDetectionFingerprintMinutes is set to {0}",
                    options.IntroSkipOptions.IntroDetectionFingerprintMinutes);
                Logger.Info("ParallelMarkerComputation is set to {0}",
                    options.IntroSkipOptions.ParallelMarkerComputation);
                Logger.Info("TidbApiKey is set to {0}",
                    string.IsNullOrWhiteSpace(options.IntroSkipOptions.TidbApiKey) ? "EMPTY" : "***");
            }
            _currentUnlockIntroSkip = options.IntroSkipOptions.UnlockIntroSkip;
            _currentEnableAutomaticIntroScraping = options.IntroSkipOptions.EnableAutomaticIntroScraping;
            FingerprintApi.UpdateLibraryPathsInScope();
            FingerprintApi.UpdateLibraryIntroDetectionFingerprintLength();

            if (!suppress)
            {
                Logger.Info("MergeMultiVersion is set to {0}", options.ExperienceEnhanceOptions.MergeMultiVersion);
                Logger.Info("MergeMoviesPreference is set to {0}",
                    EnumExtensions.GetDescription(options.ExperienceEnhanceOptions.MergeMoviesPreference));
            }
            _currentMergeMultiVersion = options.ExperienceEnhanceOptions.MergeMultiVersion;

            if (!suppress)
            {
                Logger.Info("EnablePinyinSearch is set to {0}", options.PinyinEnhanceOptions.EnablePinyinSearch);
                Logger.Info("EnablePinyinSortName is set to {0}", options.PinyinEnhanceOptions.EnablePinyinSortName);
                var pinyinLibraryScope = string.Join(", ",
                    options.PinyinEnhanceOptions.LibraryScope
                        ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => options.PinyinEnhanceOptions.LibraryList
                            .FirstOrDefault(option => option.Value == v)
                            ?.Name) ?? Enumerable.Empty<string>());
                Logger.Info("Pinyin - LibraryScope is set to {0}",
                    string.IsNullOrEmpty(pinyinLibraryScope) ? "ALL" : pinyinLibraryScope);
            }

            var pinyinSearch = options.PinyinEnhanceOptions.EnablePinyinSearch;
            var pinyinSortName = options.PinyinEnhanceOptions.EnablePinyinSortName;
            if (_currentPinyinSearch != pinyinSearch || _currentPinyinSortName != pinyinSortName)
            {
                _currentPinyinSearch = pinyinSearch;
                _currentPinyinSortName = pinyinSortName;

                if (pinyinSearch || pinyinSortName)
                {
                    _pinyinScheduler?.Start();
                }
                else
                {
                    _pinyinScheduler?.Stop();
                }
            }

            if (suppress) _currentSuppressOnOptionsSaved = false;

            base.OnOptionsSaved(options);
        }

        /// <summary>
        ///     Pro: CustomEncoderPath. When a non-empty path is configured, tries (via
        ///     reflection, soft-fail) to point Emby's FfmpegConfiguration
        ///     (UseCustomEncoderPath/CustomEncoderPath) at it. On any failure it only logs,
        ///     so this stays an "option + log" feature on incompatible Emby builds.
        /// </summary>
        private void ApplyCustomEncoderPath(string customEncoderPath, bool suppress)
        {
            if (string.IsNullOrWhiteSpace(customEncoderPath))
            {
                return;
            }

            if (!File.Exists(customEncoderPath))
            {
                Logger.Info("CustomEncoderPath configured but the file does not exist: {0}", customEncoderPath);
                return;
            }

            try
            {
                var ffmpegManager = _ffmpegManager;
                if (ffmpegManager == null)
                {
                    Logger.Info(
                        "Custom ffmpeg path configured, but the FFmpeg manager is unavailable on this Emby version");
                    return;
                }

                var configurationProperty = ffmpegManager.GetType().GetProperty("FfmpegConfiguration");
                var configuration = configurationProperty?.GetValue(ffmpegManager);
                if (configuration == null)
                {
                    Logger.Info(
                        "Custom ffmpeg path configured, but FfmpegConfiguration is unavailable on this Emby version");
                    return;
                }

                var useCustomProperty = configuration.GetType().GetProperty("UseCustomEncoderPath");
                var pathProperty = configuration.GetType().GetProperty("CustomEncoderPath");
                if (useCustomProperty?.CanWrite != true || pathProperty?.CanWrite != true)
                {
                    Logger.Info(
                        "Custom ffmpeg path configured, but runtime override not supported on this Emby version");
                    return;
                }

                useCustomProperty.SetValue(configuration, true);
                pathProperty.SetValue(configuration, customEncoderPath);
                Logger.Info("Custom ffmpeg path applied: {0}", customEncoderPath);
            }
            catch (Exception ex)
            {
                Logger.Info("Custom ffmpeg path configured, but runtime override failed: {0}", ex.Message);
                Logger.Debug(ex.StackTrace);
            }
        }

        protected override PluginOptions OnBeforeShowUI(PluginOptions options)
        {
            PopulateUIOptions(options);
            return base.OnBeforeShowUI(options);
        }

        /// <summary>
        ///     设置页数据源填充（下拉列表、免责声明、冲突状态行、版本信息）。
        ///     从 OnBeforeShowUI 抽出：分 Tab 后每个 Tab 视图构造时也要调用，
        ///     否则对应 MultilSelect 的选项列表为空。
        /// </summary>
        internal void PopulateUIOptions(PluginOptions options)
        {
            options.Disclaimer.Clear();
            options.Disclaimer.Add(
                new GenericListItem
                {
                    PrimaryText = Resources.DisclaimerButtonText,
                    Icon = IconNames.privacy_tip,
                    IconMode = ItemListIconMode.SmallRegular,
                    HyperLink = "https://github.com/Mr54233/YingYi#声明"
                });

            if (options.ShowConflictPluginLoadedStatus)
            {
                options.ConflictPluginLoadedStatus.Caption = Resources
                    .PluginOptions_IncompatibleMessage_Please_uninstall_the_conflict_plugin_Strm_Extract;
                options.ConflictPluginLoadedStatus.StatusText = string.Empty;
                options.ConflictPluginLoadedStatus.Status = ItemStatus.Warning;
            }
            else
            {
                options.ConflictPluginLoadedStatus.Caption = string.Empty;
                options.ConflictPluginLoadedStatus.StatusText = string.Empty;
                options.ConflictPluginLoadedStatus.Status = ItemStatus.None;
            }

            var libraries = _libraryManager.GetVirtualFolders();

            var list = new List<EditorSelectOption>();
            var listShow = new List<EditorSelectOption>();
            var listMarkerEnabled = new List<EditorSelectOption>();

            list.Add(new EditorSelectOption
            {
                Value = "-1",
                Name = Resources.Favorites,
                IsEnabled = true
            });

            listMarkerEnabled.Add(new EditorSelectOption
            {
                Value = "-1",
                Name = Resources.Favorites,
                IsEnabled = true
            });

            foreach (var item in libraries)
            {
                if (ExcludedCollectionTypes.Contains(item.CollectionType))
                {
                    continue;
                }

                var selectOption = new EditorSelectOption
                {
                    Value = item.ItemId,
                    Name = item.Name,
                    IsEnabled = true,
                };

                list.Add(selectOption);

                if (item.CollectionType == "tvshows" || item.CollectionType is null)
                {
                    listShow.Add(selectOption);

                    if (item.LibraryOptions.EnableMarkerDetection)
                    {
                        listMarkerEnabled.Add(selectOption);
                    }
                }
            }

            options.MediaInfoExtractOptions.LibraryList = list;
            options.IntroSkipOptions.LibraryList = listShow;
            options.IntroSkipOptions.MarkerEnabledLibraryList = listMarkerEnabled;
            options.PinyinEnhanceOptions.LibraryList = list;

            var catchTaskList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(CatchupTask)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = EnumExtensions.GetDescription(item),
                    IsEnabled = true,
                };

                catchTaskList.Add(selectOption);
            }

            options.GeneralOptions.CatchupTaskList = catchTaskList;

            var persistOptionList = new List<EditorRadioOption>
            {
                new EditorRadioOption
                {
                    Value = PersistMediaInfoOption.Default,
                    PrimaryText = Resources.MediaInfoExtractOptions_PersistMediaInfo_Persist_MediaInfo,
                    SecondaryText =
                        Resources
                            .MediaInfoExtractOptions_PersistMediaInfo_Persist_media_info_in_JSON_file__Default_is_OFF_
                },
                new EditorRadioOption
                {
                    Value = PersistMediaInfoOption.Restore,
                    PrimaryText = Resources.MediaInfoExtractOptions_MediaInfoRestoreMode_MediaInfo_Restore_Mode,
                    SecondaryText =
                        Resources
                            .MediaInfoExtractOptions_MediaInfoRestoreMode_Only_restore_media_info__chapters__and_video_thumbnails_from_JSON_or_BIF__skipping_extraction__Default_is_OFF_
                },
                new EditorRadioOption
                {
                    Value = PersistMediaInfoOption.None,
                    PrimaryText = Resources.PersistMediaInfoOption_None_None
                }
            };

            options.MediaInfoExtractOptions.PersistMediaInfoOptionList = persistOptionList;

            var episodeRefreshOptionList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(EpisodeRefreshOption)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = EnumExtensions.GetDescription(item),
                    IsEnabled = true,
                };

                episodeRefreshOptionList.Add(selectOption);
            }

            options.MetadataEnhanceOptions.EpisodeRefreshOptionList = episodeRefreshOptionList;

            var refreshPersonOptionList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(RefreshPersonOption)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = EnumExtensions.GetDescription(item),
                    IsEnabled = true,
                };

                refreshPersonOptionList.Add(selectOption);
            }

            options.MetadataEnhanceOptions.RefreshPersonOptionList = refreshPersonOptionList;

            var introSkipPreferenceList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(IntroSkipPreference)))
            {
                var selectPreference = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = EnumExtensions.GetDescription(item),
                    IsEnabled = true,
                };

                introSkipPreferenceList.Add(selectPreference);
            }

            options.IntroSkipOptions.IntroSkipPreferenceList = introSkipPreferenceList;

            var searchScopeList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(PinyinEnhanceOptions.SearchItemScopeOption)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = EnumExtensions.GetDescription(item),
                    IsEnabled = true,
                };

                searchScopeList.Add(selectOption);
            }

            options.PinyinEnhanceOptions.SearchScopeList = searchScopeList;

            options.AboutOptions.VersionInfoList.Clear();
            options.AboutOptions.VersionInfoList.Add(
                new GenericListItem
                {
                    PrimaryText = GetVersionHash(),
                    Icon = IconNames.info,
                    IconMode = ItemListIconMode.SmallRegular
                });

            options.AboutOptions.VersionInfoList.Add(
                new GenericListItem
                {
                    PrimaryText = Resources.Repo_Link,
                    Icon = IconNames.code,
                    IconMode = ItemListIconMode.SmallRegular,
                    HyperLink = "https://github.com/Mr54233/YingYi",
                });

            options.AboutOptions.VersionInfoList.Add(
                new GenericListItem
                {
                    PrimaryText = Resources.Wiki_Link,
                    Icon = IconNames.menu_book,
                    IconMode = ItemListIconMode.SmallRegular,
                    HyperLink = "https://github.com/Mr54233/YingYi#readme",
                });

            var allUsers = LibraryApi.AllUsers;
            var userList = new List<EditorSelectOption>();
            foreach (var user in allUsers)
            {
                var selectOption = new EditorSelectOption
                {
                    Value = user.Key.InternalId.ToString(),
                    Name = (user.Value ? "\ud83d\udc51" : "\ud83d\udc64") + user.Key.Name,
                    IsEnabled = true,
                };

                userList.Add(selectOption);
            }

            options.IntroSkipOptions.UserList = userList;
        }

        protected override void OnCreatePageInfo(PluginPageInfo pageInfo)
        {
            pageInfo.Name = Name;
            pageInfo.DisplayName =
                Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant", DefaultUICulture);
            pageInfo.EnableInMainMenu = true;
            pageInfo.MenuIcon = "video_settings";

            base.OnCreatePageInfo(pageInfo);
        }

        private static string GetVersionHash()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            var fullVersion = assembly.GetName().Version?.ToString();

            if (informationalVersion != null)
            {
                var parts = informationalVersion.Split('+');
                var shortCommitHash = parts.Length > 1 ? parts[1].Substring(0, 7) : "n/a";
                return $"{fullVersion}+{shortCommitHash}";
            }

            return fullVersion;
        }
    }
}

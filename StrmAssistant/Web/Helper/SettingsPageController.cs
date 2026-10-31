using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Web.GenericEdit;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Options;

namespace StrmAssistant.Web.Helper
{
    /// <summary>
    ///     影医设置页 Tab 化（对齐 Pro 版行为）。
    ///     架构：Plugin 重新实现 IHasUIPages（接管页面注册）；
    ///     唯一宿主控制器 SettingsPageController 实现 IHasTabbedUIPages，
    ///     其 TabPageControllers 提供 5 个 tab（基础/元数据/片头片尾/体验与网络/关于）。
    ///     每个 tab 的 ContentData 直接是 PluginOptions 的对应 section（EditableOptionsBase）；
    ///     保存时读全量 → 只赋回本节 → 走 Plugin 的 internal 桥触发 base SaveOptions 事件链。
    ///     JSON 文件名（影医.json）、字段形状、PageId=c2f533:Settings 全部不变。
    /// </summary>
    internal class SettingsPageController : IPluginUIPageController, IHasTabbedUIPages
    {
        private readonly IPlugin _plugin;
        private readonly PluginInfo _pluginInfo;
        private readonly ILogger _logger;
        private readonly Func<PluginOptions> _getOptions;
        private readonly Action<PluginOptions> _saveOptions;
        private readonly Action<PluginOptions> _populateUI;

        public SettingsPageController(IPlugin plugin, PluginInfo pluginInfo, ILogger logger,
            Func<PluginOptions> getOptions, Action<PluginOptions> saveOptions, Action<PluginOptions> populateUI)
        {
            _plugin = plugin;
            _pluginInfo = pluginInfo;
            _logger = logger;
            _getOptions = getOptions;
            _saveOptions = saveOptions;
            _populateUI = populateUI;
            PluginId = pluginInfo.Id;

            PageInfo = new PluginPageInfo
            {
                Name = "Settings",
                EnableInMainMenu = true,
                DisplayName = pluginInfo.Name,
                MenuIcon = "video_settings",
                IsMainConfigPage = true
            };

            TabPageControllers = new List<IPluginUIPageController>
            {
                new SectionTabController(pluginInfo, "General", "基础设置", "tune",
                    () => Section(getOptions(), o => o.GeneralOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "MediaInfo", "媒体信息提取", "movie",
                    () => Section(getOptions(), o => o.MediaInfoExtractOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Pinyin", "拼音增强", "translate",
                    () => Section(getOptions(), o => o.PinyinEnhanceOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Metadata", "元数据增强", "auto_awesome",
                    () => Section(getOptions(), o => o.MetadataEnhanceOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Rating", "评分聚合", "star",
                    () => Section(getOptions(), o => o.MetadataRefreshOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Cache", "元数据缓存", "storage",
                    () => Section(getOptions(), o => o.MetadataBuildOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Scrape", "抓取规则", "travel_explore",
                    () => Section(getOptions(), o => o.MetadataScrapeOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "IntroSkip", "片头片尾探测", "skip_next",
                    () => Section(getOptions(), o => o.IntroSkipOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Experience", "体验增强", "auto_fix_high",
                    () => Section(getOptions(), o => o.ExperienceEnhanceOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "UIFunctions", "UI 功能", "web",
                    () => Section(getOptions(), o => o.UIFunctionOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "Network", "网络", "lan",
                    () => Section(getOptions(), o => o.ServerOptions), _populateUI,
                    () => pluginInfo),
                new SectionTabController(pluginInfo, "About", "关于", "info",
                    () => Section(getOptions(), o => o.AboutOptions), _populateUI,
                    () => pluginInfo)
            };
        }

        private static IEditableObject Section(PluginOptions o, Func<PluginOptions, IEditableObject> pick)
            => pick(o);

        public string PluginId { get; }

        public PluginPageInfo PageInfo { get; }

        public IReadOnlyList<IPluginUIPageController> TabPageControllers { get; }

        public Task Initialize(CancellationToken token) => Task.CompletedTask;

        public Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new SettingsOverviewView(_pluginInfo, _getOptions, _saveOptions, _populateUI);
            return Task.FromResult(view);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Options;
using StrmAssistant.Properties;
using Emby.Web.GenericEdit.Common;

namespace StrmAssistant.Web.Helper
{
    /// <summary>每个 Tab 的视图：ContentData = 对应 section，保存只写回本节。</summary>
    internal class SectionTabController : IPluginUIPageController
    {
        private readonly PluginInfo _pluginInfo;
        private readonly string _displayName;
        private readonly string _menuIcon;
        private readonly Func<IEditableObject> _section;
        private readonly Action<PluginOptions> _populateUI;
        private readonly Func<PluginInfo> _pluginInfoProvider;

        public SectionTabController(PluginInfo pluginInfo, string name, string displayName, string menuIcon,
            Func<IEditableObject> section, Action<PluginOptions> populateUI, Func<PluginInfo> pluginInfoProvider)
        {
            _pluginInfo = pluginInfo;
            _displayName = displayName;
            _menuIcon = menuIcon;
            _section = section;
            _populateUI = populateUI;
            _pluginInfoProvider = pluginInfoProvider;

            PageInfo = new PluginPageInfo
            {
                Name = "YingYi" + name,
                EnableInMainMenu = false,
                DisplayName = displayName,
                MenuIcon = menuIcon,
                IsMainConfigPage = false
            };
        }

        public PluginPageInfo PageInfo { get; }

        public string PluginId => _pluginInfo.Id;

        public Task Initialize(CancellationToken token) => Task.CompletedTask;

        public Task<IPluginUIView> CreateDefaultPageView()
        {
            // BasePluginSimpleUI.OnBeforeShowUI belongs to the private default controller;
            // once we re-register UIPageControllers, each tab must populate its own option lists.
            _populateUI(Plugin.Instance.GetPluginOptions());
            IPluginUIView view = new SectionTabView(_pluginInfo.Id, _section, _populateUI, _pluginInfoProvider, _displayName);
            return Task.FromResult(view);
        }
    }

    /// <summary>Tab 页视图：本节可编辑；保存 = 读全量 → 赋回本节 → Plugin internal 桥。</summary>
    internal class SectionTabView : PluginPageViewBase
    {
        private readonly Func<IEditableObject> _section;
        private readonly Action<PluginOptions> _populateUI;
        private readonly Func<PluginInfo> _pluginInfoProvider;
        private readonly string _displayName;

        public SectionTabView(string pluginId, Func<IEditableObject> section,
            Action<PluginOptions> populateUI, Func<PluginInfo> pluginInfoProvider, string displayName)
            : base(pluginId)
        {
            _section = section;
            _populateUI = populateUI;
            _pluginInfoProvider = pluginInfoProvider;
            _displayName = displayName;
            ContentData = section();
        }

        public override string Caption => _displayName;

        public override Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            var section = ContentData;

            // 与 Emby 内置 PluginOptionsPageView.OnSaveCommand 一致：先跑节内自定义校验
            // （ValidateOrThrow 会触发 Validate(ValidationContext)，如缓存模式启用必填根目录、
            // 抓取规则合法性等），失败抛 ValidationException → 宿主转 400/错误提示。
            ((EditableObjectBase)section).ValidateOrThrow();

            // 交给 Plugin 的 internal 桥：读全量（含其它节）→ 赋回本节 → base.SaveOptions 事件链
            Plugin.Instance.ApplySection(section, _populateUI);

            // 重建视图以显示最新状态
            IPluginUIView view = new SectionTabView(PluginId, _section, _populateUI, _pluginInfoProvider, _displayName);
            return Task.FromResult(view);
        }
    }

    /// <summary>
    ///     第 0 个 tab：概览页。ContentData 必须是非空 EditableOptionsBase，否则前端渲染空白。
    ///     除版本/导航提示外，还承载根级免责声明与冲突插件警告（原单页 UI 渲染的
    ///     PluginOptions.Disclaimer / ConflictPluginLoadedStatus，Tab 化后根级属性无处显示，
    ///     审查指出安全提示从 UI 消失属功能性回退）。
    /// </summary>
    internal class SettingsOverviewOptions : EditableOptionsBase
    {
        public Emby.Web.GenericEdit.Elements.List.GenericItemList ModuleStatusList { get; set; } =
            new Emby.Web.GenericEdit.Elements.List.GenericItemList();

        public StatusItem ConflictStatus { get; set; } = new StatusItem();

        public SettingsOverviewOptions()
        {
            var plugin = Plugin.Instance;
            var version = plugin?.CurrentVersion ?? "unknown";

            ModuleStatusList.Add(new Emby.Web.GenericEdit.Elements.List.GenericListItem
            {
                PrimaryText = "当前版本：" + version,
                Icon = IconNames.info,
                IconMode = ItemListIconMode.SmallRegular
            });
            ModuleStatusList.Add(new Emby.Web.GenericEdit.Elements.List.GenericListItem
            {
                PrimaryText = "使用上方标签页切换各设置分组；本页为概览，不含可编辑字段",
                Icon = IconNames.help_outline,
                IconMode = ItemListIconMode.SmallRegular
            });

            // 根级免责声明（GPL 声明/日志外发警告）——从 Plugin 填充
            var disclaimer = plugin?.GetPluginOptions()?.Disclaimer;
            if (disclaimer != null)
            {
                foreach (var item in disclaimer)
                {
                    ModuleStatusList.Add(item);
                }
            }

            ConflictStatus = new StatusItem();
        }

        public override string EditorTitle => "概览";
        public override string EditorDescription => "影医 YingYi 设置概览";
    }

    internal class SettingsOverviewView : PluginPageViewBase
    {
        public SettingsOverviewView(PluginInfo pluginInfo, Func<PluginOptions> getOptions,
            Action<PluginOptions> saveOptions, Action<PluginOptions> populateUI)
            : base(pluginInfo.Id)
        {
            ShowSave = false;
            var options = new SettingsOverviewOptions();
            populateUI(Plugin.Instance.GetPluginOptions());

            // 冲突插件状态（StrmExtract/InfuseSync 同装时显示警告）——与原单页同源
            var root = Plugin.Instance.GetPluginOptions();
            if (root.ShowConflictPluginLoadedStatus)
            {
                options.ConflictStatus.Caption = Resources.PluginOptions_IncompatibleMessage_Please_uninstall_the_conflict_plugin_Strm_Extract;
                options.ConflictStatus.Status = ItemStatus.Warning;
            }

            ContentData = options;
        }
    }
}

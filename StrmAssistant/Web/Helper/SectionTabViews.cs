using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Options;

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

            // 交给 Plugin 的 internal 桥：读全量（含其它节）→ 赋回本节 → base.SaveOptions 事件链
            Plugin.Instance.ApplySection(section, _populateUI);

            // 重建视图以显示最新状态
            IPluginUIView view = new SectionTabView(PluginId, _section, _populateUI, _pluginInfoProvider, _displayName);
            return Task.FromResult(view);
        }
    }

    /// <summary>总览 tab：只读展示插件信息与各分组导航说明。</summary>
    internal class SettingsOverviewView : PluginPageViewBase
    {
        public SettingsOverviewView(PluginInfo pluginInfo, Func<PluginOptions> getOptions,
            Action<PluginOptions> saveOptions, Action<PluginOptions> populateUI)
            : base(pluginInfo.Id)
        {
            ShowSave = false;
        }
    }
}

using System;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Plugins.UI.Views.Enums;

namespace StrmAssistant.Web.Helper
{
    /// <summary>
    ///     影医设置 Tab 视图基类（对齐 ViewMate 的 PluginPageView 结构，适配 IPluginUIView /
    ///     IPluginViewWithOptions / IPluginPageView 三个接口，4.8 编译面与 4.10 运行面同构）。
    /// </summary>
    internal abstract class PluginPageViewBase : IPluginUIView, IPluginViewWithOptions, IPluginPageView
    {
        protected PluginPageViewBase(string pluginId)
        {
            PluginId = pluginId;
        }

        public event EventHandler<GenericEventArgs<IPluginUIView>> UIViewInfoChanged
        {
            add { }
            remove { }
        }

        public string PluginId { get; }

        public IEditableObject ContentData { get; set; }

        public UserDto User { get; set; }

        public string RedirectViewUrl { get; set; }

        public virtual string Caption => ContentData?.EditorTitle ?? string.Empty;

        public virtual string SubCaption => ContentData?.EditorDescription ?? string.Empty;

        public bool ShowSave { get; set; } = true;

        public bool ShowBack { get; set; } = false;

        public bool AllowSave { get; set; } = true;

        public bool AllowBack { get; set; } = true;

        public virtual bool IsCommandAllowed(string commandKey) => true;

        public virtual Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            // 宿主只在拿到 null 时才转去调 OnSaveCommand（ViewMate 语义）
            return Task.FromResult<IPluginUIView>(null);
        }

        public virtual Task Cancel() => Task.CompletedTask;

        public virtual void OnDialogResult(IPluginUIView dialogView, bool completedOk, object data) { }

        public virtual Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            return Task.FromResult<IPluginUIView>(this);
        }

        public PluginViewOptions ViewOptions => new PluginViewOptions();
    }
}

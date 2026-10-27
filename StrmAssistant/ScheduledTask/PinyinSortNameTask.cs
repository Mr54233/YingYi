using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    ///     P1-2 manual entry point: writes the pinyin initials into MediaItems.SortName
    ///     while backing up the previous value in PinyinSortNameBackup.
    /// </summary>
    public class PinyinSortNameTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;

        public PinyinSortNameTask()
        {
            _logger = Plugin.Instance.Logger;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("PinyinSortName - Task Execute");
            await Task.Yield();
            progress.Report(0);

            if (!Plugin.Instance.GetPluginOptions().PinyinEnhanceOptions.EnablePinyinSortName)
            {
                _logger.Info("PinyinSortName - Task skipped, EnablePinyinSortName is off");
                progress.Report(100);
                return;
            }

            var service = Plugin.Instance.PinyinScheduler?.SortNameService;
            if (service == null)
            {
                _logger.Warn("PinyinSortName - Task aborted, pinyin layer not initialised");
                progress.Report(100);
                return;
            }

            await Task.Run(() => service.ApplyPending(progress, cancellationToken), cancellationToken);

            progress.Report(100);
            _logger.Info("PinyinSortName - Task Complete");
        }

        public string Category => Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
            Plugin.Instance.DefaultUICulture);

        public string Key => "PinyinSortNameTask";

        public string Description => Resources.ResourceManager.GetString(
            "PinyinSortNameTask_Description_Generate_pinyin_initials_as_sort_title_and_back_up_the_original_value",
            Plugin.Instance.DefaultUICulture);

        public string Name => Resources.ResourceManager.GetString(
            "PinyinSortNameTask_Name_Pinyin_Sort_Title", Plugin.Instance.DefaultUICulture);

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}

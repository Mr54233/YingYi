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
    ///     P1-2 rollback: restores every backed up SortName and then drops
    ///     PinyinSortNameBackup so the library returns to the exact state it had before
    ///     the feature was ever enabled (plan §4.2).
    /// </summary>
    public class PinyinSortNameRollbackTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;

        public PinyinSortNameRollbackTask()
        {
            _logger = Plugin.Instance.Logger;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("PinyinSortName - Rollback Task Execute");
            await Task.Yield();
            progress.Report(0);

            // Rollback is intentionally NOT gated on EnablePinyinSortName: the user may
            // have just turned the switch off and wants the library restored right now.
            var service = Plugin.Instance.PinyinScheduler?.SortNameService;
            if (service == null)
            {
                _logger.Info("PinyinSortName - Rollback aborted, pinyin layer not initialised (nothing to do)");
                progress.Report(100);
                return;
            }

            await Task.Run(() => service.Rollback(progress, cancellationToken), cancellationToken);

            progress.Report(100);
            _logger.Info("PinyinSortName - Rollback Task Complete");
        }

        public string Category => Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
            Plugin.Instance.DefaultUICulture);

        public string Key => "PinyinSortNameRollbackTask";

        public string Description => Resources.ResourceManager.GetString(
            "PinyinSortNameRollbackTask_Description_Restore_original_sort_titles_and_drop_the_backup_table",
            Plugin.Instance.DefaultUICulture);

        public string Name => Resources.ResourceManager.GetString(
            "PinyinSortNameRollbackTask_Name_Rollback_Pinyin_Sort_Title", Plugin.Instance.DefaultUICulture);

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}

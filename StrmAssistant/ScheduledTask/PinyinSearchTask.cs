using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Pinyin;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    ///     P1-1 manual entry point. Walks every MediaItems row whose FTS document still
    ///     holds the plain name and injects the pinyin token string built by
    ///     <see cref="PinyinConverter.BuildSearchTokens"/>.
    /// </summary>
    public class PinyinSearchTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;

        public PinyinSearchTask()
        {
            _logger = Plugin.Instance.Logger;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("PinyinSearch - Inject Task Execute");
            await Task.Yield();
            progress.Report(0);

            if (!Plugin.Instance.GetPluginOptions().PinyinEnhanceOptions.EnablePinyinSearch)
            {
                _logger.Info("PinyinSearch - Inject Task skipped, EnablePinyinSearch is off");
                progress.Report(100);
                return;
            }

            var writer = Plugin.Instance.PinyinScheduler?.FtsWriter;
            if (writer == null)
            {
                _logger.Warn("PinyinSearch - Inject Task aborted, pinyin layer not initialised");
                progress.Report(100);
                return;
            }

            await Task.Run(() => writer.InjectPending(progress, cancellationToken), cancellationToken);

            progress.Report(100);
            _logger.Info("PinyinSearch - Inject Task Complete");
        }

        public string Category => Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
            Plugin.Instance.DefaultUICulture);

        public string Key => "PinyinSearchInjectTask";

        public string Description => Resources.ResourceManager.GetString(
            "PinyinSearchTask_Description_Inject_pinyin_search_tokens_into_the_Emby_search_index",
            Plugin.Instance.DefaultUICulture);

        public string Name => Resources.ResourceManager.GetString(
            "PinyinSearchTask_Name_Pinyin_Search_Index", Plugin.Instance.DefaultUICulture);

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}

using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Properties;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    public class RestartServerTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;
        private readonly IServerApplicationHost _serverApplicationHost;

        public RestartServerTask(IServerApplicationHost serverApplicationHost)
        {
            _serverApplicationHost = serverApplicationHost;
            _logger = Plugin.Instance.Logger;
        }

        public string Key => "RestartServerTask";

        public string Name => "Restart YingYi Server";

        public string Description => Resources.ResourceManager.GetString(
            "RestartServerTask_Description_Restarts_the_server_after_a_3_second_delay",
            Plugin.Instance.DefaultUICulture);

        public string Category => Resources.ResourceManager.GetString(
            "PluginOptions_EditorTitle_Strm_Assistant", Plugin.Instance.DefaultUICulture);

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("RestartServer - Scheduled Task Execute");

            var canSelfRestart = false;
            try { canSelfRestart = _serverApplicationHost.CanSelfRestart; }
            catch (Exception e) { _logger.Warn("RestartServer - Failed to read CanSelfRestart: " + e.Message); }

            if (!canSelfRestart)
            {
                _logger.Warn("RestartServer - This instance cannot self-restart; aborting");
                progress.Report(100);
                return Task.CompletedTask;
            }

            _logger.Info("RestartServer - Restarting in 3 seconds");
            Task.Run(() =>
            {
                try { Thread.Sleep(3000); _serverApplicationHost.Restart(); }
                catch (Exception e)
                {
                    Plugin.Instance.Logger.Error("RestartServer - Restart failed: " + e.Message);
                }
            });
            progress.Report(100);
            return Task.CompletedTask;
        }

        public System.Collections.Generic.IEnumerable<TaskTriggerInfo> GetDefaultTriggers() { return Array.Empty<TaskTriggerInfo>(); }

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;
    }
}

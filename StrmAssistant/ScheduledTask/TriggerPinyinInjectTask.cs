using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    ///     P1-1 / P1-2 second timing layer (plan §3.2): runs the incremental pass right
    ///     after a library scan finished. Emby discovers every
    ///     <see cref="ILibraryPostScanTask"/> implementation in the plugin assembly, so
    ///     this needs no explicit registration.
    /// </summary>
    public class TriggerPinyinInjectTask : ILibraryPostScanTask
    {
        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            try
            {
                Plugin.Instance?.PinyinScheduler?.OnLibraryScanCompleted();
            }
            catch
            {
                // never break the scan pipeline because of the pinyin layer
            }

            return Task.CompletedTask;
        }
    }
}

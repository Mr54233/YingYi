using System;
using System.Linq;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    ///     Pure match rule for the SuppressPluginUpdates list (P3-5). Kept free of any
    ///     Emby or Plugin dependency so it can be unit-tested without a live release —
    ///     the release freeze forbids publishing one just to reach this branch.
    /// </summary>
    internal static class UpdateSuppressRule
    {
        /// <summary>
        ///     A comma/semicolon separated list hits when an entry equals the plugin
        ///     assembly filename, equals the remote tag, or is a case-insensitive prefix
        ///     of the filename. Returns the winning entry (for logging) or null.
        /// </summary>
        internal static string FindSuppressHit(string suppressList, string assemblyFilename, string remoteTag)
        {
            var entries = (suppressList ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0);

            return entries.FirstOrDefault(n =>
                string.Equals(n, assemblyFilename, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, remoteTag, StringComparison.OrdinalIgnoreCase) ||
                (assemblyFilename ?? string.Empty).StartsWith(n, StringComparison.OrdinalIgnoreCase));
        }
    }
}

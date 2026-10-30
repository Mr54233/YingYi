using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using StrmAssistant.Options;

namespace StrmAssistant.Web.Helper
{
    internal static class ScriptInjectHelper
    {
        private static readonly ConcurrentDictionary<string, byte[]> Assets =
            new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public static MemoryStream GetResourceStream(string resourceName)
        {
            var bytes = Assets.GetOrAdd(resourceName, LoadResource);
            return new MemoryStream(bytes, writable: false);
        }

        public static bool IsEnabled(string optionKey)
        {
            var options = Plugin.Instance?.GetPluginOptions()?.UIFunctionOptions;
            if (options == null) return false;
            switch (optionKey)
            {
                case nameof(UIFunctionOptions.BeautifyMissingMetadata): return options.BeautifyMissingMetadata;
                case nameof(UIFunctionOptions.EnforceLibraryOrder): return options.EnforceLibraryOrder;
                case nameof(UIFunctionOptions.EnhanceMissingEpisodes): return options.EnhanceMissingEpisodes;
                case nameof(UIFunctionOptions.LibraryScopePlayProgress): return options.LibraryScopePlayProgress;
                case nameof(UIFunctionOptions.MarkPreviousEpisodesPlayed): return options.MarkPreviousEpisodesPlayed;
                case nameof(UIFunctionOptions.NoBoxsetsAutoCreation): return options.NoBoxsetsAutoCreation;
                case nameof(UIFunctionOptions.OptimizeMediaItemOrder): return options.OptimizeMediaItemOrder;
                case nameof(UIFunctionOptions.ReplaceUnplayedWithTotal): return options.ReplaceUnplayedWithTotal;
                case nameof(UIFunctionOptions.ReverseLogLines): return options.ReverseLogLines;
                case nameof(UIFunctionOptions.ShowCollectionsFromSeasons): return options.ShowCollectionsFromSeasons;
                case nameof(UIFunctionOptions.EnforceUserDisplayOption): return options.EnforceUserDisplayOption;
                default: return false;
            }
        }

        public static object DisabledResult(IHttpResultFactory resultFactory, MediaBrowser.Model.Services.IRequest request)
        {
            // Emby 4.10 maps a thrown HttpException to generic 500. Set the response
            // directly so script loaders receive a real HTTP 404 gate response.
            if (request?.Response != null)
            {
                request.Response.StatusCode = (int)HttpStatusCode.NotFound;
                request.Response.StatusDescription = "Not Found";
                request.Response.ContentType = "text/plain";
            }

            return resultFactory.GetResult(request, ReadOnlyMemory<byte>.Empty, "text/plain", null);
        }

        public static byte[] GetResourceBytes(string resourceName)
        {
            var bytes = Assets.GetOrAdd(resourceName, LoadResource);
            return (byte[])bytes.Clone();
        }

        private static byte[] LoadResource(string resourceName)
        {
            var name = typeof(Plugin).Namespace + ".Web.Resources." + resourceName;
            using (var stream = typeof(ScriptInjectHelper).GetTypeInfo().Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Embedded UI resource was not found", name);
                using (var destination = new MemoryStream())
                {
                    stream.CopyTo(destination);
                    return destination.ToArray();
                }
            }
        }
    }
}

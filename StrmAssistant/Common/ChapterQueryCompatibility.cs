using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;

namespace StrmAssistant.Common
{
    /// <summary>
    ///     Emby 4.9 dropped <c>IItemRepository.GetChapters(long, MarkerType[])</c> and replaced it with
    ///     <c>GetChapters(long, MarkerType[], CancellationToken)</c>. The 1-argument
    ///     <c>GetChapters(BaseItem)</c> and the 2-argument <c>GetChapters(BaseItem, CancellationToken)</c>
    ///     overloads exist on 4.8, 4.9 and 4.10 alike.
    ///     This helper resolves the best available marker-filtered overload once and falls back to
    ///     loading all chapters in memory when no marker-filtered overload exists (e.g. 4.8.x).
    /// </summary>
    internal static class ChapterQueryCompatibility
    {
        private static readonly object Sync = new object();

        private static bool _resolved;
        private static MethodInfo _byIdAndMarkersWithToken;
        private static MethodInfo _byIdAndMarkers;

        private static void Resolve(IItemRepository repository)
        {
            if (_resolved) return;

            lock (Sync)
            {
                if (_resolved) return;

                var type = repository.GetType();

                _byIdAndMarkersWithToken = ReflectionMethodResolver.FindExactMethod(
                    type,
                    new[] { "GetChapters" },
                    typeof(List<ChapterInfo>),
                    new[] { typeof(long), typeof(MarkerType[]), typeof(CancellationToken) });

                _byIdAndMarkers = _byIdAndMarkersWithToken is null
                    ? ReflectionMethodResolver.FindExactMethod(
                        type,
                        new[] { "GetChapters" },
                        typeof(List<ChapterInfo>),
                        new[] { typeof(long), typeof(MarkerType[]) })
                    : null;

                _resolved = true;

                if (_byIdAndMarkersWithToken is null && _byIdAndMarkers is null && Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"{nameof(ChapterQueryCompatibility)}: no marker-filtered GetChapters overload on {type.FullName}, " +
                        "falling back to in-memory filtering");
                }
            }
        }

        internal static List<ChapterInfo> GetChapters(IItemRepository repository, BaseItem item,
            params MarkerType[] markerTypes)
        {
            Resolve(repository);

            if (_byIdAndMarkersWithToken != null)
            {
                return (List<ChapterInfo>)_byIdAndMarkersWithToken.Invoke(repository,
                    new object[] { item.InternalId, markerTypes, CancellationToken.None })!;
            }

            if (_byIdAndMarkers != null)
            {
                return (List<ChapterInfo>)_byIdAndMarkers.Invoke(repository,
                    new object[] { item.InternalId, markerTypes })!;
            }

            var all = repository.GetChapters(item);

            if (markerTypes.Length == 0) return all;

            return all.Where(chapter => Array.IndexOf(markerTypes, chapter.MarkerType) >= 0).ToList();
        }

        internal static bool HasAny(IItemRepository repository, BaseItem item, MarkerType markerType)
        {
            return GetChapters(repository, item, markerType).Count > 0;
        }

        internal static long? GetMarkerStart(IItemRepository repository, BaseItem item, MarkerType markerType)
        {
            var chapter = GetChapters(repository, item, markerType).FirstOrDefault();

            return chapter?.StartPositionTicks;
        }
    }
}

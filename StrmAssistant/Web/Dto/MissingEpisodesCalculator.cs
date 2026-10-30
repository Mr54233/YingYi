using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Web.Dto
{
    /// <summary>
    ///     Pure expected-vs-local episode diff behind the P3-4 MissingEpisodes endpoint.
    ///     Kept free of any Emby dependency so it can be unit-tested directly.
    /// </summary>
    public static class MissingEpisodesCalculator
    {
        public static List<MissingEpisode> ComputeMissing(int seasonNumber,
            IEnumerable<int> expectedEpisodeNumbers, IEnumerable<int> localEpisodeNumbers)
        {
            var local = new HashSet<int>(localEpisodeNumbers ?? Enumerable.Empty<int>());
            return (expectedEpisodeNumbers ?? Enumerable.Empty<int>())
                .Where(number => number > 0 && !local.Contains(number))
                .Distinct()
                .OrderBy(number => number)
                .Select(number => new MissingEpisode { SeasonNumber = seasonNumber, EpisodeNumber = number })
                .ToList();
        }
    }
}

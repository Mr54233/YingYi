using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Rating;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Douban
{
    /// <summary>
    ///     Thin coordinator that turns an item's IMDb id into a Douban subject id by
    ///     delegating to the existing WikiData bridge (Rating.WikiDataClient.ResolveDoubanIdAsync).
    ///     The SPARQL logic lives in exactly one place; this class only normalizes input
    ///     and never throws.
    /// </summary>
    public class DoubanIdResolver
    {
        private readonly ILogger _logger;
        private readonly WikiDataClient _wikiDataClient;

        public DoubanIdResolver(ILogger logger, IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _wikiDataClient = new WikiDataClient(logger, httpClient, jsonSerializer);
        }

        public DoubanIdResolver(WikiDataClient wikiDataClient)
        {
            _logger = Plugin.Instance?.Logger;
            _wikiDataClient = wikiDataClient;
        }

        /// <summary>
        ///     Resolves a Douban subject id for the given item via its IMDb provider id.
        ///     Returns null when the item has no IMDb id, the bridge fails, or the result
        ///     looks malformed. Never throws; cancellation propagates only when the caller
        ///     cancelled the token.
        /// </summary>
        public async Task<string> ResolveByItemAsync(BaseItem item, CancellationToken ct)
        {
            if (item == null || item.ProviderIds == null)
            {
                return null;
            }

            string imdbId = null;
            try
            {
                if (!item.ProviderIds.TryGetValue("Imdb", out imdbId) || string.IsNullOrWhiteSpace(imdbId))
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger?.Debug("Douban - Failed to read IMDb id for {0}: {1}", item.Name, e.Message);
                return null;
            }

            try
            {
                return await _wikiDataClient.ResolveDoubanIdAsync(imdbId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger?.Debug("Douban - WikiData bridge failed for {0}: {1}", item.Name, e.Message);
                return null;
            }
        }
    }
}

using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using StrmAssistant.Properties;

namespace StrmAssistant.Provider
{
    /// <summary>
    ///     Pro: registers "YingYiPersist" as a displayable external id type so that media
    ///     info persisted by this plugin can surface as an external identifier on the item
    ///     detail page. This only registers the id type (visible in metadata settings);
    ///     actually writing ProviderIds entries is intentionally NOT done in this batch
    ///     (it requires scraper integration).
    /// </summary>
    public class PersistMediaInfoProvider : IExternalId
    {
        public string Name => Resources.PersistMediaInfoProvider_Name_YingYi_Persist;

        public string Key => "YingYiPersist";

        public string UrlFormatString => "https://github.com/Mr54233/YingYi";

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is Episode;
        }
    }
}

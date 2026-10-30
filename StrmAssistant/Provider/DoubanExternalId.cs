using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using StrmAssistant.Properties;

namespace StrmAssistant.Provider
{
    /// <summary>
    ///     Registers "Douban" as a displayable external id type, mirroring the Bangumi
    ///     external id. Supports Movie / Series / Season (NOT Episode, matching the
    ///     Emby-Plugin-Douban Pro semantics where the id binds to whole works).
    ///     This only registers the id type; populating the ids is done by
    ///     PopulateDoubanIdTask / the scrape service.
    /// </summary>
    public class DoubanExternalId : IExternalId
    {
        public string Name => Resources.DoubanExternalId_Name_Douban;

        public string Key => "Douban";

        public string UrlFormatString => "https://movie.douban.com/subject/{0}/";

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is Series || item is Season;
        }
    }
}

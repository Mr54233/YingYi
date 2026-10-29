using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using StrmAssistant.Properties;

namespace StrmAssistant.Provider
{
    public class BangumiExternalId : IExternalId
    {
        public string Name => Resources.BangumiExternalId_Name_Bangumi;

        public string Key => "Bangumi";

        public string UrlFormatString => "https://bgm.tv/subject/{0}";

        public bool Supports(IHasProviderIds item)
        {
            return item is Series || item is Movie;
        }
    }
}

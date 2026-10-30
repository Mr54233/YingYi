using StrmAssistant.Common;
using StrmAssistant.Options;

namespace StrmAssistant.Metadata
{
    /// <summary>
    /// 组B：TMDb 备选配置解析与可用性判定。
    /// 【重要】实际改写 TMDb 请求未实现：真正的请求改写需要 Harmony patch Emby 的
    /// MovieDbSeriesProvider/MovieDbMovieProvider 内部请求逻辑，红线禁止使用 Harmony，
    /// 因此本服务只负责把设置页的备选配置解析成「生效 URL/Key/图片基址」，
    /// 供后续接线（或运行时验证）调用，不拦截、不改写任何网络请求。
    /// </summary>
    public static class MovieDbAlternativeService
    {
        private const string DefaultAlternativeApiUrl = "https://api.tmdb.org";
        private const string SystemDefaultApiUrl = "https://api.themoviedb.org";

        /// <summary>开关开启且配置了合法备选 URL 时返回备选；否则返回 false（沿用系统默认）。</summary>
        public static bool TryGetEffectiveApiUrl(out string url)
        {
            var options = GetOptions();
            url = null;

            if (options == null || !options.EnableAltMovieDbUrl)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.AltMovieDbApiUrl))
            {
                // 开关开着但 URL 留空：按说明默认用 https://api.tmdb.org
                url = DefaultAlternativeApiUrl;
                return true;
            }

            if (!CommonUtility.IsValidHttpUrl(options.AltMovieDbApiUrl))
            {
                Plugin.Instance.Logger.Warn(
                    "MovieDbAlternativeService: invalid AltMovieDbApiUrl '{0}', falling back to system default", options.AltMovieDbApiUrl);
                return false;
            }

            url = options.AltMovieDbApiUrl.TrimEnd('/');
            return true;
        }

        /// <summary>开关开启且填了 Key 时返回备选 Key；否则返回 false（沿用系统默认）。</summary>
        public static bool TryGetEffectiveApiKey(out string apiKey)
        {
            var options = GetOptions();
            apiKey = null;

            if (options == null || !options.EnableAltMovieDbUrl)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.AltMovieDbApiKey))
            {
                return false;
            }

            apiKey = options.AltMovieDbApiKey.Trim();
            return true;
        }

        /// <summary>开关开启且填了图片基址时返回备选；否则返回 false（沿用系统默认）。</summary>
        public static bool TryGetEffectiveImageUrl(out string imageUrl)
        {
            var options = GetOptions();
            imageUrl = null;

            if (options == null || !options.EnableAltMovieDbUrl)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.AltMovieDbImageUrl))
            {
                return false;
            }

            if (!CommonUtility.IsValidHttpUrl(options.AltMovieDbImageUrl))
            {
                Plugin.Instance.Logger.Warn(
                    "MovieDbAlternativeService: invalid AltMovieDbImageUrl '{0}', falling back to system default", options.AltMovieDbImageUrl);
                return false;
            }

            imageUrl = options.AltMovieDbImageUrl.TrimEnd('/');
            return true;
        }

        private static MetadataScrapeOptions GetOptions()
        {
            try
            {
                return Plugin.Instance.GetPluginOptions().MetadataScrapeOptions;
            }
            catch
            {
                // 插件尚未初始化完成时 Plugin.Instance 可能为 null，降级为「无备选配置」
                return null;
            }
        }
    }
}

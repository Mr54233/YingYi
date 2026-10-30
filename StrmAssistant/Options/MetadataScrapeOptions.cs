using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Common;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 组B：元数据抓取规则选项（TMDb 备选地址 / TVDB 中文 / 语言过滤 / 剧集分组）。
    /// 红线说明：真正的 TMDb 请求改写需要 Harmony patch Emby 内部请求管线，本插件禁止使用
    /// Harmony，因此本类与 MovieDbAlternativeService 只提供「配置解析与可用性判定」，
    /// 实际改写 TMDb 请求未实现。选项先行就位，供后续运行时验证与接线使用。
    /// </summary>
    public class MetadataScrapeOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataScrapeOptions_EditorTitle_Scrape_Rules", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataScrapeOptions_EditorTitle_Scrape_Rules;

        [DisplayNameL("MetadataScrapeOptions_EnableAltMovieDbUrl_Enable_Alternative_MovieDb_URL", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_EnableAltMovieDbUrl_Use_alternative_TMDb_API_URL_image_URL_and_API_key_instead_of_the_system_defaults__Default_is_False_", typeof(Resources))]
        [Required]
        public bool EnableAltMovieDbUrl { get; set; } = false;

        [DisplayNameL("MetadataScrapeOptions_AltMovieDbApiUrl_Alternative_MovieDb_API_URL", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_AltMovieDbApiUrl_Default_alternative_is_https___api_tmdb_org__Blank_uses_system_default_https___api_themoviedb_org_", typeof(Resources))]
        [VisibleCondition(nameof(EnableAltMovieDbUrl), SimpleCondition.IsTrue)]
        public string AltMovieDbApiUrl { get; set; } = string.Empty;

        [DisplayNameL("MetadataScrapeOptions_AltMovieDbApiKey_Alternative_MovieDb_API_Key", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_AltMovieDbApiKey_API_key_used_with_the_alternative_TMDb_endpoint__Blank_uses_system_default_", typeof(Resources))]
        [IsPassword]
        [VisibleCondition(nameof(EnableAltMovieDbUrl), SimpleCondition.IsTrue)]
        public string AltMovieDbApiKey { get; set; } = string.Empty;

        [DisplayNameL("MetadataScrapeOptions_AltMovieDbImageUrl_Alternative_MovieDb_Image_URL", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_AltMovieDbImageUrl_Base_URL_for_TMDb_images__Blank_uses_system_default_", typeof(Resources))]
        [VisibleCondition(nameof(EnableAltMovieDbUrl), SimpleCondition.IsTrue)]
        public string AltMovieDbImageUrl { get; set; } = string.Empty;

        [DisplayNameL("MetadataScrapeOptions_ChineseTvdb_Chinese_TVDB", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_ChineseTvdb_When_set__TVDB_metadata_is_preferred_in_Chinese_languages__Default_is_False_", typeof(Resources))]
        [Required]
        public bool ChineseTvdb { get; set; } = false;

        [DisplayNameL("MetadataScrapeOptions_BlockNonFallbackLanguage_Block_Non_Fallback_Languages", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_BlockNonFallbackLanguage_Only_keep_metadata_in_MovieDb_TVDB_fallback_languages__Default_is_False_", typeof(Resources))]
        [Required]
        public bool BlockNonFallbackLanguage { get; set; } = false;

        [DisplayNameL("MetadataScrapeOptions_LocalEpisodeGroup_Local_Episode_Group", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_LocalEpisodeGroup_Use_local_absolute_episode_numbering_for_TVDB_episodes__Default_is_False_", typeof(Resources))]
        [Required]
        public bool LocalEpisodeGroup { get; set; } = false;

        [DisplayNameL("MetadataScrapeOptions_MovieDbEpisodeGroup_MovieDb_Episode_Group", typeof(Resources))]
        [DescriptionL("MetadataScrapeOptions_MovieDbEpisodeGroup_Use_the_TMDb_episode_group_endpoint_instead_of_the_default_season_episodes__Default_is_False_", typeof(Resources))]
        [Required]
        public bool MovieDbEpisodeGroup { get; set; } = false;

        protected override void Validate(ValidationContext context)
        {
            if (EnableAltMovieDbUrl && !string.IsNullOrWhiteSpace(AltMovieDbApiUrl) &&
                !CommonUtility.IsValidHttpUrl(AltMovieDbApiUrl))
            {
                context.AddValidationError(Resources.InvalidAltMovieDbApiUrl);
            }
        }
    }
}

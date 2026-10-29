using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class IntroSkipOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_IntroSkipOptions_Intro_Credits_Detection", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_IntroSkipOptions_Intro_Credits_Detection;
        
        [DisplayNameL("IntroSkipOptions_UnlockIntroSkip_Built_in_Intro_Skip_Enhanced", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_UnlockIntroSkip_Unlock_Strm_support_for_built_in_intro_skip_detection", typeof(Resources))]
        [Required]
        public bool UnlockIntroSkip { get; set; } = false;

        [DisplayNameL("IntroSkipOptions_IntroDetectionFingerprintMinutes_Intro_Detection_Fingerprint_Minutes", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_IntroDetectionFingerprintMinutes_It_must_be_between_2_and_20__Default_is_10_", typeof(Resources))]
        [MinValue(2), MaxValue(20)]
        [Required]
        [VisibleCondition(nameof(UnlockIntroSkip), SimpleCondition.IsTrue)]
        public int IntroDetectionFingerprintMinutes { get; set; } = 10;

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> MarkerEnabledLibraryList { get; set; }

        [DisplayNameL("IntroSkipOptions_MarkerEnabledLibraryScope_Library_Scope", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_MarkerEnabledLibraryScope_Intro_detection_enabled_library_scope__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(MarkerEnabledLibraryList))]
        [VisibleCondition(nameof(UnlockIntroSkip), SimpleCondition.IsTrue)]
        [Browsable(false)]
        public string MarkerEnabledLibraryScope
        {
            get => string.Empty;
            set { }
        }

        [DisplayNameL("PluginOptions_EnableIntroSkip_Enable_Intro_Skip__Experimental_", typeof(Resources))]
        [DescriptionL("PluginOptions_EnableIntroSkip_Enable_intro_skip_and_credits_skip_for_episodes__Default_is_False_", typeof(Resources))]
        public bool EnableIntroSkip { get; set; } = false;

        [DisplayNameL("IntroSkipOptions_MaxIntroDurationSeconds", typeof(Resources))]
        [MinValue(10), MaxValue(600)]
        [Required]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public int MaxIntroDurationSeconds { get; set; } = 150;

        [DisplayNameL("IntroSkipOptions_MaxCreditsDurationSeconds", typeof(Resources))]
        [MinValue(10), MaxValue(600)]
        [Required]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public int MaxCreditsDurationSeconds { get; set; } = 360;

        [DisplayNameL("IntroSkipOptions_MinOpeningPlotDurationSeconds", typeof(Resources))]
        [MinValue(30), MaxValue(120)]
        [Required]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public int MinOpeningPlotDurationSeconds { get; set; } = 60;

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> LibraryList { get; set; }

        [DisplayNameL("IntroSkipOptions_LibraryScope_Library_Scope", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_LibraryScope_TV_shows_library_scope_to_detect__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public string LibraryScope { get; set; } = string.Empty;

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> UserList { get; set; }

        [DisplayNameL("IntroSkipOptions_UserScope_User_Scope", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_UserScope_Users_allowed_to_detect__Blank_includes_all", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(UserList))]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public string UserScope { get; set; } = string.Empty;

        [DisplayNameL("IntroSkipOptions_ClientScope_Client_Scope", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_ClientScope_Allowed_clients__Default_is_Emby_Infuse_SenPlayer", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public string ClientScope { get; set; } = "Emby,Infuse,SenPlayer";

        public enum IntroSkipPreference
        {
            [DescriptionL("IntroSkipControl_ResetAndOverwrite_ResetAndOverwrite", typeof(Resources))]
            ResetAndOverwrite,
            [DescriptionL("IntroSkipPreference_NoDetectionButReset_NoDetectionButReset", typeof(Resources))]
            NoDetectionButReset
        }

        [Browsable(false)]
        public List<EditorSelectOption> IntroSkipPreferenceList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("IntroSkipOptions_IntroSkipPreferences_IntroSkip_Preferences", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(IntroSkipPreferenceList))]
        [VisibleCondition(nameof(EnableIntroSkip), SimpleCondition.IsTrue)]
        public string IntroSkipPreferences { get; set; } = string.Empty;

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> CommunityIntroSourceList { get; set; } = new List<EditorSelectOption>
        {
            new EditorSelectOption { Value = "IntroDb", Name = "IntroDB (introdb.app)", IsEnabled = true },
            new EditorSelectOption { Value = "TheIntroDb", Name = "TheIntroDB (theintrodb.org)", IsEnabled = true }
        };

        [DisplayNameL("IntroSkipOptions_EnableCommunityIntroSource_Community_Intro_Source", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_EnableCommunityIntroSource_Fill_missing_intro_markers_from_community_database__Default_is_False_", typeof(Resources))]
        [Required]
        public bool EnableCommunityIntroSource { get; set; } = false;

        [DisplayNameL("IntroSkipOptions_CommunityIntroSources_Community_Intro_Sources", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_CommunityIntroSources_Comma_separated_community_intro_sources__Default_is_IntroDb_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(CommunityIntroSourceList))]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public string CommunityIntroSources { get; set; } = "IntroDb";

        [DisplayNameL("IntroSkipOptions_CommunitySourceMinConfidence_Minimum_Confidence", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_CommunitySourceMinConfidence_Minimum_confidence_threshold_0_1__Default_is_0_6_", typeof(Resources))]
        [MinValue(0), MaxValue(1)]
        [Required]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public double CommunitySourceMinConfidence { get; set; } = 0.6;

        [DisplayNameL("IntroSkipOptions_CommunitySourceRequestIntervalMs_Request_Interval_Milliseconds", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_CommunitySourceRequestIntervalMs_Interval_between_community_source_requests__Default_is_1100_", typeof(Resources))]
        [MinValue(0), MaxValue(60000)]
        [Required]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public int CommunitySourceRequestIntervalMs { get; set; } = 1100;

        [DisplayNameL("IntroSkipOptions_CommunitySourceTimeoutSeconds_Request_Timeout_Seconds", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_CommunitySourceTimeoutSeconds_Community_source_request_timeout__Default_is_10_", typeof(Resources))]
        [MinValue(1), MaxValue(120)]
        [Required]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public int CommunitySourceTimeoutSeconds { get; set; } = 10;

        [DisplayNameL("IntroSkipOptions_CommunitySourceCacheDays_Negative_Result_Cache_Days", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_CommunitySourceCacheDays_Cache_days_for_no_intro_verdicts__Default_is_30_", typeof(Resources))]
        [MinValue(1), MaxValue(365)]
        [Required]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public int CommunitySourceCacheDays { get; set; } = 30;

        [DisplayNameL("IntroSkipOptions_CommunitySourceRequireManualConfirm_Require_Manual_Confirm", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_CommunitySourceRequireManualConfirm_Only_write_markers_after_manual_verification__Default_is_False_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public bool CommunitySourceRequireManualConfirm { get; set; } = false;

        /// <summary>
        /// Pro: TheIntroDB API Key。非空时会以 "Api-Key" 请求头发给 api.theintrodb.org，
        /// 用于提升该社区源的请求限额。
        /// </summary>
        [DisplayNameL("IntroSkipOptions_TidbApiKey_TheIntroDB_API_Key", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_TidbApiKey_Optional_API_key_for_TheIntroDB_to_raise_the_rate_limit__Leave_empty_for_anonymous_access_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnableCommunityIntroSource), SimpleCondition.IsTrue)]
        public string TidbApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Pro: 季内并行计算片头标记。开启后单个季内部的各集指纹→标记计算会并行执行
        /// （MaxDegreeOfParallelism = ProcessorCount-1）；季与季之间本来就并行，不受此开关影响。
        /// </summary>
        [DisplayNameL("IntroSkipOptions_ParallelMarkerComputation_Parallel_Marker_Computation", typeof(Resources))]
        [DescriptionL("IntroSkipOptions_ParallelMarkerComputation_Compute_intro_markers_for_episodes_within_a_season_in_parallel__Default_is_False_", typeof(Resources))]
        [Required]
        public bool ParallelMarkerComputation { get; set; } = false;

        protected override void Validate(ValidationContext context)
        {
            if (EnableCommunityIntroSource && !UnlockIntroSkip)
            {
                context.AddValidationError(Resources.ResourceManager.GetString(
                    "InvalidCommunityIntroSource", Plugin.Instance.DefaultUICulture) ?? "Community Intro Source requires Intro Skip Enhanced to be unlocked first.");
            }
        }
    }
}

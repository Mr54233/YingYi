using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class GeneralOptions : EditableOptionsBase
    {
        [DisplayNameL("GeneralOptions_EditorTitle_General_Options", typeof(Resources))]
        public override string EditorTitle => Resources.GeneralOptions_EditorTitle_General_Options;

        [DisplayNameL("PluginOptions_CatchupMode_Catch_up_Mode__Experimental_", typeof(Resources))]
        [DescriptionL("PluginOptions_CatchupMode_Catch_up_users_favorites__exclusive_to_Strm___Default_is_False_", typeof(Resources))]
        [Required]
        public bool CatchupMode { get; set; } = false;

        public enum CatchupTask
        {
            [DescriptionL("CatchupTask_MediaInfo_MediaInfo", typeof(Resources))]
            MediaInfo,
            [DescriptionL("CatchupTask_Fingerprint_Fingerprint", typeof(Resources))]
            Fingerprint,
            [DescriptionL("CatchupTask_IntroSkip_IntroSkip", typeof(Resources))]
            IntroSkip,
            [DescriptionL("CatchupTask_EpisodeRefresh_EpisodeRefresh", typeof(Resources))]
            EpisodeRefresh
        }
        
        [Browsable(false)]
        public IEnumerable<EditorSelectOption> CatchupTaskList { get; set; }

        [DisplayNameL("GeneralOptions_CatchupScope_Catchup_Scope", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(CatchupTaskList))]
        [VisibleCondition(nameof(CatchupMode), SimpleCondition.IsTrue)]
        public string CatchupTaskScope { get; set; } = CatchupTask.MediaInfo.ToString();

        [DisplayNameL("PluginOptions_MaxConcurrentCount_Max_Concurrent_Count", typeof(Resources))]
        [DescriptionL("PluginOptions_MaxConcurrentCount_Max_Concurrent_Count_must_be_between_1_to_10__Default_is_1_", typeof(Resources))]
        [Required, MinValue(1), MaxValue(20)]
        public int MaxConcurrentCount { get; set; } = 1;

        [DisplayNameL("GeneralOptions_CooldownSeconds_Cooldown_Time__Seconds___Default_is_0", typeof(Resources))]
        [DescriptionL("GeneralOptions_CooldownDurationSeconds_Applicable_to_single_thread_mode__Default_is_0_", typeof(Resources))]
        [VisibleCondition(nameof(MaxConcurrentCount), ValueCondition.IsEqual, 1)]
        [Required, MinValue(0), MaxValue(60)]
        public int CooldownDurationSeconds { get; set; } = 0;
        
        [DisplayNameL("GeneralOptions_Tier2MaxConcurrentCount_Tier_2_Max_Concurrent_Count", typeof(Resources))]
        [DescriptionL("GeneralOptions_Tier2MaxConcurrentCount_Refresh_metadata__subtitle__local_tasks__Default_is_1_", typeof(Resources))]
        [Required, MinValue(1), MaxValue(20)]
        public int Tier2MaxConcurrentCount { get; set; } = 1;

        /// <summary>
        /// Pro: 自定义 ffmpeg 编码器路径（绝对路径）。
        /// 留空则完全交给 Emby 自身设置。非空时插件会在启动/保存选项时尝试通过反射把
        /// Emby 的 FfmpegConfiguration（UseCustomEncoderPath/CustomEncoderPath）指向该路径，
        /// 反射失败（不同 Emby 版本字段变化）时仅记录 Info 日志，不做其他事。
        /// </summary>
        [DisplayNameL("GeneralOptions_CustomEncoderPath_Custom_ffmpeg_Path", typeof(Resources))]
        [DescriptionL("GeneralOptions_CustomEncoderPath_Absolute_path_to_a_custom_ffmpeg_binary__Leave_empty_to_use_the_Emby_server_setting_", typeof(Resources))]
        [Required]
        public string CustomEncoderPath { get; set; } = string.Empty;
    }
}

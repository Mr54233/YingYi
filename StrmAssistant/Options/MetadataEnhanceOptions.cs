using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public enum RefreshPersonOption
    {
        [DescriptionL("RefreshPersonOption_Default_Default", typeof(Resources))]
        Default,
        [DescriptionL("RefreshPersonOption_FullRefresh_Full_Refresh", typeof(Resources))]
        FullRefresh,
        [DescriptionL("RefreshPersonOption_NoAdult_Skip_Adult_Entries", typeof(Resources))]
        NoAdult
    }

    public class MetadataEnhanceOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_MetadataEnhanceOptions_Metadata_Enhance", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_MetadataEnhanceOptions_Metadata_Enhance;
        
        [DisplayNameL("MetadataEnhanceOptions_RefreshPersonMode_Refresh_Person_Mode", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_RefreshPersonMode_Person_metadata_refresh_mode_for_the_scheduled_task__Default_is_Default_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(RefreshPersonOptionList))]
        [Required]
        public string RefreshPersonMode { get; set; } = RefreshPersonOption.Default.ToString();

        [Browsable(false)]
        public List<EditorSelectOption> RefreshPersonOptionList { get; set; } = new List<EditorSelectOption>();

        public enum EpisodeRefreshOption
        {
            [DescriptionL("EpisodeRefreshOption_NoOverview_No_Overview", typeof(Resources))]
            NoOverview,
            [DescriptionL("EpisodeRefreshOption_NoImage_No_Image", typeof(Resources))]
            NoImage,
            [DescriptionL("EpisodeRefreshOption_NonChineseOverview_Non_Chinese_Overview", typeof(Resources))]
            NonChineseOverview,
            [DescriptionL("EpisodeRefreshOption_DefaultEpisodeName_Default_Episode_Name", typeof(Resources))]
            DefaultEpisodeName,
            [DescriptionL("EpisodeRefreshOption_ReplaceCapturedImage_Replace_Captured_Image", typeof(Resources))]
            ReplaceCapturedImage
        }

        [Browsable(false)]
        public List<EditorSelectOption> EpisodeRefreshOptionList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("MetadataEnhanceOptions_EpisodeRefreshScope_Episode_Metadata_Refresh_Scope", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EpisodeRefreshScope_Episode_refresh_scope_for_scheduled_task_and_catch_up__Default_is_no_overview_and_no_image_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(EpisodeRefreshOptionList))]
        public string EpisodeRefreshScope { get; set; } = string.Join(",", EpisodeRefreshOption.NoOverview.ToString(),
            EpisodeRefreshOption.NoImage.ToString());
        
        [DisplayNameL("MetadataEnhanceOptions_EpisodeRefreshLookBackDays_Episode_Refresh_Lookback_Days", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EpisodeRefreshLookbackDays_Episode_metadata_refresh_lookback_days__Default_is_365_", typeof(Resources))]
        [Required, MinValue(1)]
        public int EpisodeRefreshLookbackDays { get; set; } = 365;

        [DisplayNameL("ModOptions_HidePersonNoImage_Hide_Person_without_Image", typeof(Resources))]
        [DescriptionL("ModOptions_HidePersonNoImage_Auto_hide_cast_members_without_a_primary_image_on_movie_and_series_pages__The_delete_person_task_applies_this_preference_when_deleting__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool HidePersonNoImage { get; set; } = false;

        public enum HidePersonOption
        {
            [DescriptionL("HidePersonOption_All_All", typeof(Resources))]
            All,
            [DescriptionL("HidePersonOption_NoImageOnly_No_Image_Only", typeof(Resources))]
            NoImageOnly
        }

        [DisplayNameL("MetadataEnhanceOptions_HidePersonPreference_Hide_Person_Preference", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_HidePersonPreference_Which_persons_the_delete_task_targets_when_hiding_is_enabled__No_Image_Only_targets_persons_without_a_primary_image__Default_is_No_Image_Only_", typeof(Resources))]
        [VisibleCondition(nameof(HidePersonNoImage), SimpleCondition.IsTrue)]
        public string HidePersonPreference { get; set; } = HidePersonOption.NoImageOnly.ToString();
    }
}

using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class UIFunctionOptions : EditableOptionsBase
    {
        public override string EditorTitle => Resources.UIFunctionOptions_EditorTitle_UI_Functions;

        [DisplayNameL("UIFunctionOptions_BeautifyMissingMetadata_Beautify_Missing_Metadata", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_BeautifyMissingMetadata_Beautify_missing_metadata_for_episode_display__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool BeautifyMissingMetadata { get; set; }

        [DisplayNameL("UIFunctionOptions_EnforceLibraryOrder_Enforce_Library_Order", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_EnforceLibraryOrder_Enforce_library_order_per_the_first_admin__Default_if_OFF_", typeof(Resources))]
        [Required]
        public bool EnforceLibraryOrder { get; set; }

        [DisplayNameL("UIFunctionOptions_EnhanceMissingEpisodes_Missing_Episodes_Enhanced", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_EnhanceMissingEpisodes_Add_MovieDb_support_for_the_missing_episodes_function__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnhanceMissingEpisodes { get; set; }

        [DisplayNameL("UIFunctionOptions_LibraryScopePlayProgress_Library_Scope_Play_Progress", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_LibraryScopePlayProgress_Keep_play_progress_separate_per_library__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool LibraryScopePlayProgress { get; set; }

        [DisplayNameL("UIFunctionOptions_MarkPreviousEpisodesPlayed_Mark_Previous_Episodes_Played", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_MarkPreviousEpisodesPlayed_Mark_previous_episodes_as_played__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool MarkPreviousEpisodesPlayed { get; set; }

        [DisplayNameL("UIFunctionOptions_NoBoxsetsAutoCreation_Prevent_Boxsets_Auto_Creation", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_NoBoxsetsAutoCreation_Prevent_automatic_boxset_library_creation__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool NoBoxsetsAutoCreation { get; set; }

        [DisplayNameL("UIFunctionOptions_OptimizeMediaItemOrder_Optimize_Media_Item_Order", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_OptimizeMediaItemOrder_Optimize_media_item_display_order__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool OptimizeMediaItemOrder { get; set; }

        [DisplayNameL("UIFunctionOptions_ReplaceUnplayedWithTotal_Replace_Unplayed_With_Total", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_ReplaceUnplayedWithTotal_Replace_unplayed_count_with_total_count__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool ReplaceUnplayedWithTotal { get; set; }

        [DisplayNameL("UIFunctionOptions_ReverseLogLines_Reverse_Log_Lines", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_ReverseLogLines_Show_newest_log_lines_first__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool ReverseLogLines { get; set; }

        [DisplayNameL("UIFunctionOptions_ShowCollectionsFromSeasons_Show_Collections_From_Seasons", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_ShowCollectionsFromSeasons_Show_collections_from_seasons__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool ShowCollectionsFromSeasons { get; set; }

        public enum ItemOrderOption
        {
            Default,
            ReleaseDate,
            Name,
            DateAdded
        }

        [DisplayNameL("UIFunctionOptions_ItemOrderOption_Item_Order", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_ItemOrderOption_Item_order_used_by_the_list_enhancement__Default_is_Default_", typeof(Resources))]
        public ItemOrderOption ItemOrder { get; set; } = ItemOrderOption.Default;

        [DisplayNameL("UIFunctionOptions_MarkPreviousEpisodesPlayedThreshold_Threshold", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_MarkPreviousEpisodesPlayedThreshold_Number_of_previous_episodes_to_mark__Default_is_0_", typeof(Resources))]
        [MinValue(0)]
        public int MarkPreviousEpisodesPlayedThreshold { get; set; }

        [DisplayNameL("UIFunctionOptions_EnforceUserDisplayOption_Enforce_User_Display", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_EnforceUserDisplayOption_Enforce_user_display_for_missing_episodes__Default_is_OFF_", typeof(Resources))]
        public bool EnforceUserDisplayOption { get; set; }
    }
}

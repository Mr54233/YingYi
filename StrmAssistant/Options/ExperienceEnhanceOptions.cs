using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class ExperienceEnhanceOptions : EditableOptionsBase
    {
        [DisplayNameL("ExperienceEnhanceOptions_EditorTitle_Experience_Enhance", typeof(Resources))]
        public override string EditorTitle => Resources.ExperienceEnhanceOptions_EditorTitle_Experience_Enhance;
        
        [DisplayNameL("GeneralOptions_MergeMultiVersion_Merge_Multiple_Versions", typeof(Resources))]
        [DescriptionL("GeneralOptions_MergeMultiVersion_Auto_merge_multiple_versions_if_in_the_same_folder_", typeof(Resources))]
        [Required]
        public bool MergeMultiVersion { get; set; } = false;

        public enum MergeMoviesScopeOption
        {
            [DescriptionL("MergeScopeOption_LibraryScope_LibraryScope", typeof(Resources))]
            LibraryScope,
            [DescriptionL("MergeScopeOption_GlobalScope_GlobalScope", typeof(Resources))]
            GlobalScope
        }
        
        [DisplayName("")]
        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        public MergeMoviesScopeOption MergeMoviesPreference { get; set; } = MergeMoviesScopeOption.LibraryScope;

        public enum MergeSeriesScopeOption
        {
            [DescriptionL("MergeScopeOption_LibraryScope_LibraryScope", typeof(Resources))]
            LibraryScope,
            [DescriptionL("MergeScopeOption_GlobalScope_GlobalScope", typeof(Resources))]
            GlobalScope
        }

        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        [Browsable(false)]
        public MergeSeriesScopeOption MergeSeriesPreference => MergeSeriesScopeOption.LibraryScope;

        [DisplayNameL("ExperienceEnhanceOptions_EnableExternalTrackScan_Enable_External_Track_Scan", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnableExternalTrackScan_Scan_libraries_for_external_audio_track_files_with_the_same_name_as_videos__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableExternalTrackScan { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_ExternalTrackFormats_External_Track_Formats", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_ExternalTrackFormats_Audio_file_extensions_treated_as_external_tracks__Comma_separated__Default_is_mka_ac3_eac3_", typeof(Resources))]
        public string ExternalTrackFormats { get; set; } = "mka,ac3,eac3";

        [Browsable(false)]
        public string MultiVersionDisplayMode { get; set; } = "Default";

        [Browsable(false)]
        public string MultiVersionSortMode { get; set; } = "Default";

        [DisplayNameL("ExperienceEnhanceOptions_EnableDeepDelete_Enable_Deep_Delete", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnableDeepDelete_Attempt_to_cascade_delete_the_underlying_file_of_strm_or_symlink__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableDeepDelete { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_SuppressPluginUpdates_Suppress_Auto_Plugin_Updates", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_SuppressPluginUpdates_Plugin_names_separated_by_comma_or_semicolon_like_MovieDb_Tvdb__Default_is_BLANK_", typeof(Resources))]
        public string SuppressPluginUpdates { get; set; } = string.Empty;
    }
}

using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class MetadataRefreshOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataRefreshOptions_EditorTitle_Rating", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataRefreshOptions_EditorTitle_Rating;

        [DisplayNameL("Rating_EnableRatingAggregation_Enable_Rating_Aggregation", typeof(Resources))]
        [DescriptionL("Rating_EnableRatingAggregation_Enable_Rating_Aggregation_Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableRatingAggregation { get; set; } = false;

        [DisplayNameL("Rating_MdbListApiKey_MDBList_API_Key", typeof(Resources))]
        [DescriptionL("Rating_MdbListApiKey_Optional_API_key_for_MDBList__Leave_blank_to_skip_this_source_", typeof(Resources))]
        public string MdbListApiKey { get; set; } = string.Empty;

        [DisplayNameL("Rating_OmdbApiKey_OMDb_API_Key", typeof(Resources))]
        [DescriptionL("Rating_OmdbApiKey_Optional_API_key_for_OMDb__Leave_blank_to_skip_this_source_", typeof(Resources))]
        public string OmdbApiKey { get; set; } = string.Empty;

        [DisplayNameL("Rating_RatingUpdateLookbackDays_Rating_Update_Lookback_Days", typeof(Resources))]
        [DescriptionL("Rating_RatingUpdateLookbackDays_Only_update_ratings_for_items_released_within_this_many_days__Default_is_365_", typeof(Resources))]
        [Required]
        public int RatingUpdateLookbackDays { get; set; } = 365;
    }
}

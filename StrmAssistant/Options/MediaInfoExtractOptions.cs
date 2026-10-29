using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using MediaBrowser.Model.MediaInfo;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class MediaInfoExtractOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_EditorTitle_Strm_Extract", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_EditorTitle_Strm_Extract;

        [DisplayNameL("PluginOptions_IncludeExtra_Include_Extra", typeof(Resources))]
        [DescriptionL("PluginOptions_IncludeExtra_Include_media_extras_to_extract__Default_is_False_", typeof(Resources))]
        [Required]
        public bool IncludeExtra { get; set; } = false;

        [DisplayNameL("PluginOptions_EnableImageCapture_Enable_Image_Capture", typeof(Resources))]
        [DescriptionL("PluginOptions_EnableImageCapture_Perform_image_capture_for_videos_without_primary_image__Default_is_False_", typeof(Resources))]
        [Browsable(false)]
        [Required]
        public bool EnableImageCapture => false;

        [DisplayNameL("MediaInfoExtractOptions_ImageCaptureOffset_Image_Capture_Offset", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_ImageCaptureOffset_Image_capture_position_as_a_percentage_of_runtime__Default_is_10_", typeof(Resources))]
        [Required, MinValue(10), MaxValue(90)]
        [VisibleCondition(nameof(EnableImageCapture), SimpleCondition.IsTrue)]
        public int ImageCapturePosition { get; set; } = 10;

        /// <summary>
        /// Pro: 附加图片后处理开关。开启后截图任务会按 <see cref="ImageCaptureOffsetSeconds"/> 生成偏移截图。
        /// 语义差异说明：本插件没有"按百分比取帧"的逻辑，截图路径是章节图（Chapter images），
        /// 因此偏移量作用于每个章节图的时间点，而非按运行时百分比取帧。
        /// </summary>
        [DisplayNameL("MediaInfoExtractOptions_EnableImageCapturePostProcess_Enable_Image_Capture_Post_Process", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_EnableImageCapturePostProcess_Generate_extra_chapter_images_with_a_time_offset__Default_is_False_", typeof(Resources))]
        [Required]
        public bool EnableImageCapturePostProcess { get; set; } = false;

        /// <summary>
        /// Pro: 截图时间偏移秒数。会加到每个章节图的时间点上（clamp 到 0..RunTimeTicks）。
        /// 语义差异：作用于章节图时间点，不是按 ImageCapturePosition 百分比取帧。
        /// </summary>
        [DisplayNameL("MediaInfoExtractOptions_ImageCaptureOffsetSeconds_Image_Capture_Offset_Seconds", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_ImageCaptureOffsetSeconds_Time_offset_in_seconds_added_to_each_chapter_image_capture_position__Default_is_0_", typeof(Resources))]
        [Required, MinValue(0), MaxValue(600)]
        [VisibleCondition(nameof(EnableImageCapturePostProcess), SimpleCondition.IsTrue)]
        public int ImageCaptureOffsetSeconds { get; set; } = 0;

        [Browsable(false)]
        [Required]
        public string ImageCaptureExcludeMediaContainers { get; set; } =
            string.Join(",", new[] { MediaContainers.MpegTs, MediaContainers.Ts, MediaContainers.M2Ts });

        public enum PersistMediaInfoOption
        {
            None,
            Default,
            Restore
        }

        [Browsable(false)]
        public List<EditorRadioOption> PersistMediaInfoOptionList { get; set; } = new List<EditorRadioOption>();

        [DisplayName("")]
        [SelectItemsSource(nameof(PersistMediaInfoOptionList))]
        [SelectShowRadioGroup]
        public string PersistMediaInfoMode { get; set; } = PersistMediaInfoOption.None.ToString();

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoJsonRootFolder_MediaInfo_Json_Root_Folder", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoJsonRootFolder_Store_or_load_media_info_JSON_files_under_this_root_folder__Default_is_EMPTY_", typeof(Resources))]
        [EditFolderPicker]
        [VisibleCondition(nameof(PersistMediaInfoMode), ValueCondition.IsNotEqual, PersistMediaInfoOption.None)]
        public string MediaInfoJsonRootFolder { get; set; } = string.Empty;

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> LibraryList { get; set; }

        [DisplayNameL("PluginOptions_LibraryScope_Library_Scope", typeof(Resources))]
        [DescriptionL("PluginOptions_LibraryScope_Library_scope_to_extract__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string LibraryScope { get; set; } = string.Empty;
    }
}

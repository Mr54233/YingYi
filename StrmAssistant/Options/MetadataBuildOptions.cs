using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 组B：元数据缓存构建选项。
    /// 说明：MetadataCacheMode 仅决定「优先读本地缓存还是优先在线」；
    /// 当前版本的 Emby 元数据抓取管线尚未接线到本选项（需要 Harmony 改写内部请求，
    /// 红线禁止），本选项先行就位，供 MetadataCacheService 与后续接线使用。
    /// </summary>
    public class MetadataBuildOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataBuildOptions_EditorTitle_Metadata_Cache", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataBuildOptions_EditorTitle_Metadata_Cache;

        public enum MetadataCacheOption
        {
            [DescriptionL("MetadataCacheOption_None_Disabled", typeof(Resources))]
            None,

            [DescriptionL("MetadataCacheOption_PreferCacheThenOnline_Cache_first__fall_back_to_online", typeof(Resources))]
            PreferCacheThenOnline,

            [DescriptionL("MetadataCacheOption_PreferOnlineWithCache_Online_first__write_back_to_cache", typeof(Resources))]
            PreferOnlineWithCache,

            [DescriptionL("MetadataCacheOption_UseOverrideCacheOnly_Cache_only__never_hit_online", typeof(Resources))]
            UseOverrideCacheOnly
        }

        [Browsable(false)]
        public List<EditorSelectOption> MetadataCacheOptionList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("MetadataBuildOptions_MetadataCacheMode_Metadata_Cache_Mode", typeof(Resources))]
        [DescriptionL("MetadataBuildOptions_MetadataCacheMode_Whether_and_how_to_use_the_local_metadata_cache__Default_is_Disabled_", typeof(Resources))]
        [SelectItemsSource(nameof(MetadataCacheOptionList))]
        // Do not use SelectShowRadioGroup here: GenericEdit's radio renderer consumes
        // the inline SelectOptions array, while tab ContentData serializes only an empty
        // placeholder ({Value:"",Name:""}) and the UI displays undefined. A normal
        // select consumes ItemsSourceId from MetadataCacheOptionList correctly.
        public string MetadataCacheMode { get; set; } = MetadataCacheOption.None.ToString();

        [DisplayNameL("MetadataBuildOptions_MetadataCacheRootFolder_Cache_Root_Folder", typeof(Resources))]
        [DescriptionL("MetadataBuildOptions_MetadataCacheRootFolder_Root_folder_to_store_metadata_cache_JSON_files__Required_when_cache_mode_is_enabled_", typeof(Resources))]
        [EditFolderPicker]
        [VisibleCondition(nameof(MetadataCacheMode), ValueCondition.IsNotEqual, MetadataCacheOption.None)]
        public string MetadataCacheRootFolder { get; set; } = string.Empty;

        [DisplayNameL("MetadataBuildOptions_MetadataCacheOptionalImageTypes_Optional_Image_Types", typeof(Resources))]
        [DescriptionL("MetadataBuildOptions_MetadataCacheOptionalImageTypes_Image_types_to_include_in_the_cache_besides_the_primary_image__Default_is_EMPTY_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(OptionalImageTypeList))]
        public string MetadataCacheOptionalImageTypes { get; set; } = string.Empty;

        [Browsable(false)]
        public List<EditorSelectOption> OptionalImageTypeList { get; set; } = new List<EditorSelectOption>();

        public MetadataBuildOptions()
        {
            // 不能改 Plugin.cs 的 OnBeforeShowUI（任务书红线），因此列表在构造函数内自填。
            var cacheModeList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(MetadataCacheOption)))
            {
                cacheModeList.Add(new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = EnumExtensions.GetDescription(item),
                    IsEnabled = true
                });
            }

            MetadataCacheOptionList = cacheModeList;

            var imageTypeList = new List<EditorSelectOption>();
            foreach (Enum item in Enum.GetValues(typeof(MediaBrowser.Model.Entities.ImageType)))
            {
                imageTypeList.Add(new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = item.ToString(),
                    IsEnabled = true
                });
            }

            OptionalImageTypeList = imageTypeList;
        }

        protected override void Validate(ValidationContext context)
        {
            if (MetadataCacheMode != MetadataCacheOption.None.ToString() &&
                string.IsNullOrWhiteSpace(MetadataCacheRootFolder))
            {
                context.AddValidationError(Resources.MetadataCacheRootFolderNotExist);
            }
        }
    }
}

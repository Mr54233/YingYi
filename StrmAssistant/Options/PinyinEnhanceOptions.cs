using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class PinyinEnhanceOptions : EditableOptionsBase
    {
        [DisplayNameL("PinyinEnhanceOptions_EditorTitle_Pinyin_Enhance", typeof(Resources))]
        public override string EditorTitle => Resources.PinyinEnhanceOptions_EditorTitle_Pinyin_Enhance;

        [DisplayNameL("ModOptions_EnhanceChineseSearch_Enhance_Chinese_Search", typeof(Resources))]
        [DescriptionL(
            "ModOptions_EnhanceChineseSearch_Support_Chinese_fuzzy_search_and_Pinyin_search__Default_is_OFF_",
            typeof(Resources))]
        [Required]
        public bool EnablePinyinSearch { get; set; } = false;

        [DisplayNameL("ModOptions_PinyinSortName_Pinyin_Sort_Title", typeof(Resources))]
        [DescriptionL("ModOptions_PinyinSortName_Auto_generate_pinyin_initials_as_sort_title", typeof(Resources))]
        [Required]
        public bool EnablePinyinSortName { get; set; } = false;

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> LibraryList { get; set; }

        [DisplayNameL("ModOptions_SearchScope_Search_Scope", typeof(Resources))]
        [DescriptionL("ModOptions_SearchScope_Include_item_types__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        [VisibleCondition(nameof(IsLibraryScopeVisible), SimpleCondition.IsTrue)]
        public string LibraryScope { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsLibraryScopeVisible => EnablePinyinSearch || EnablePinyinSortName;
    }
}

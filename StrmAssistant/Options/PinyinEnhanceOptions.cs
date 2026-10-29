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

        /// <summary>
        /// Pro: 拼音索引覆盖的条目类型。默认全选（Movie/Series/Season/Episode/BoxSet）。
        /// 消费点在 Pinyin/FtsIndexWriter.cs：pending SQL 会按 MediaItems.type 过滤，
        /// 类型→type 数字映射来自 4.10 library.db 实测（BoxSet 为反编译推断值）。
        /// </summary>
        public enum SearchItemScopeOption
        {
            [DescriptionL("SearchItemScopeOption_Movie_Movie", typeof(Resources))]
            Movie,
            [DescriptionL("SearchItemScopeOption_Series_Series", typeof(Resources))]
            Series,
            [DescriptionL("SearchItemScopeOption_Season_Season", typeof(Resources))]
            Season,
            [DescriptionL("SearchItemScopeOption_Episode_Episode", typeof(Resources))]
            Episode,
            [DescriptionL("SearchItemScopeOption_BoxSet_BoxSet", typeof(Resources))]
            BoxSet
        }

        [Browsable(false)]
        public List<EditorSelectOption> SearchScopeList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("PinyinEnhanceOptions_SearchItemScope_Item_Type_Scope", typeof(Resources))]
        [DescriptionL("PinyinEnhanceOptions_SearchItemScope_Which_item_types_the_pinyin_index_covers__Default_is_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(SearchScopeList))]
        [VisibleCondition(nameof(IsLibraryScopeVisible), SimpleCondition.IsTrue)]
        public string SearchItemScope { get; set; } = string.Join(",", SearchItemScopeOption.Movie.ToString(),
            SearchItemScopeOption.Series.ToString(), SearchItemScopeOption.Season.ToString(),
            SearchItemScopeOption.Episode.ToString(), SearchItemScopeOption.BoxSet.ToString());
    }
}

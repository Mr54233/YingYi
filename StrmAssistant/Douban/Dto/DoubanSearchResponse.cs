using System.Collections.Generic;

namespace StrmAssistant.Douban.Dto
{
    /// <summary>
    ///     Response of GET https://m.douban.com/rexxar/api/v2/search/subjects?q={keyword}&amp;type={type}.
    ///     Calibrated 2026-10-29: the top-level "subjects" member is an OBJECT (not an array),
    ///     and the actual result array lives at subjects.items. Each element is
    ///     { layout, target }, so the mapping needs three levels:
    ///     DoubanSearchResponse -> DoubanSearchSubjects -> List&lt;DoubanSearchItem&gt; -> Target.
    ///     The sibling endpoint /search (without /subjects) returns mixed content
    ///     (posts/comments under "contents.items") and must never be used for item search.
    /// </summary>
    public class DoubanSearchResponse
    {
        public DoubanSearchSubjects subjects { get; set; }
    }

    /// <summary>The "subjects" object envelope: { count, total, start, items[], show_more_subjects }.</summary>
    public class DoubanSearchSubjects
    {
        public int? count { get; set; }
        public int? total { get; set; }
        public int? start { get; set; }
        public bool? show_more_subjects { get; set; }
        public List<DoubanSearchItem> items { get; set; }
    }

    /// <summary>One search hit envelope; the payload we care about is always in Target.</summary>
    public class DoubanSearchItem
    {
        public string layout { get; set; }
        public DoubanSearchTarget target { get; set; }
    }

    /// <summary>
    ///     The subject payload of a search hit. "id" is a STRING (e.g. "1292052"), never an int.
    ///     When null_rating_reason is "暂无评分", rating.value is 0 and must not be written
    ///     (same rule as the detail endpoint).
    /// </summary>
    public class DoubanSearchTarget
    {
        public string id { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string card_subtitle { get; set; }
        public string cover_url { get; set; }
        public bool? has_linewatch { get; set; }
        public string null_rating_reason { get; set; }
        public DoubanRatingDetail rating { get; set; }
    }
}

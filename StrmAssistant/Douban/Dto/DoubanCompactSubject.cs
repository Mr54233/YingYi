namespace StrmAssistant.Douban.Dto
{
    /// <summary>
    ///     SUPERSEDED (2026-10-29): this was written against a guessed search shape
    ///     (subjects being a flat array). The real rexxar response nests the payload two
    ///     levels deeper — see DoubanSearchResponse.cs (DoubanSearchSubjects -> items[] ->
    ///     DoubanSearchItem.target -> DoubanSearchTarget). Nothing references this type
    ///     anymore; it is kept only so the earlier guess stays documented. Do not use it.
    /// </summary>
    public class DoubanCompactSubject
    {
        public string id { get; set; }
        public string title { get; set; }
        public string target_type { get; set; }
        public string sub_type { get; set; }
        public string year { get; set; }
        public DoubanRatingDetail rating { get; set; }
        public string card_subtitle { get; set; }
    }
}

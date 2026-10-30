using System.Collections.Generic;
using StrmAssistant.Douban.Dto;

namespace StrmAssistant.Douban.Dto
{
    /// <summary>
    ///     Detail response of GET https://m.douban.com/rexxar/api/v2/movie/{id} (or /tv/{id}).
    ///     Field set calibrated 2026-10-29 against subject 1292052 (The Shawshank Redemption).
    ///     Property casing matches the API verbatim (snake_case keys).
    /// </summary>
    public class DoubanDetailResponse
    {
        public DoubanRatingDetail rating { get; set; }
        public string title { get; set; }
        public string original_title { get; set; }
        public string year { get; set; }
        public string type { get; set; }
        public string subtype { get; set; }
        public string intro { get; set; }
        public DoubanPicture pic { get; set; }
        public List<string> durations { get; set; }
        public List<string> genres { get; set; }
        public List<string> countries { get; set; }
        public List<DoubanCelebrity> directors { get; set; }
        public List<DoubanCelebrity> actors { get; set; }
        public List<string> aka { get; set; }
        public bool? in_blacklist { get; set; }
        public bool? is_tv { get; set; }
        public bool? has_linewatch { get; set; }
        public bool? is_released { get; set; }
        public string pubdate { get; set; }
        public int? episodes_count { get; set; }
        public string card_subtitle { get; set; }
    }
}

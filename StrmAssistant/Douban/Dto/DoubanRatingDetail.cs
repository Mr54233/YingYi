namespace StrmAssistant.Douban.Dto
{
    /// <summary>
    ///     Douban rating block. <see cref="value"/> is the Douban score on a 0-10 scale
    ///     (e.g. 9.7 for The Shawshank Redemption); <see cref="count"/> is the voter count.
    ///     Property casing matches the m.douban.com rexxar API verbatim.
    /// </summary>
    public class DoubanRatingDetail
    {
        public int? count { get; set; }
        public double? max { get; set; }
        public double? star_count { get; set; }
        public double? value { get; set; }
    }
}

namespace StrmAssistant.Douban.Dto
{
    /// <summary>
    ///     A person listed in the Douban directors/actors arrays. Only <see cref="name"/>
    ///     is consumed by this project; the remaining fields are kept nullable and unused
    ///     so unknown extra members are simply ignored by the serializer.
    /// </summary>
    public class DoubanCelebrity
    {
        public string name { get; set; }
        public string id { get; set; }
        public string roles { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string avatar { get; set; }
    }
}

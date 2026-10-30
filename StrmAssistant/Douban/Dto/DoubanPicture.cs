namespace StrmAssistant.Douban.Dto
{
    /// <summary>
    ///     Douban picture block. The detail API returns snake_case keys ("large"/"normal")
    ///     under "pic"; note the member is <c>pic.large</c> (no underscore in "pic").
    /// </summary>
    public class DoubanPicture
    {
        public string large { get; set; }
        public string normal { get; set; }
    }
}

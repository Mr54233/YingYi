using System.Collections.Generic;

namespace StrmAssistant.Rating.Dto
{
    /// <summary>
    /// WikiData Query Service SPARQL JSON result envelope.
    /// </summary>
    public class WikiDataResponse
    {
        public WikiDataHead head { get; set; }
        public WikiDataResults results { get; set; }
    }

    public class WikiDataHead
    {
        public List<string> vars { get; set; }
    }

    public class WikiDataResults
    {
        public List<Dictionary<string, WikiDataBinding>> bindings { get; set; }
    }

    /// <summary>
    /// One SPARQL variable binding (for example { "type": "literal", "value": "123" }).
    /// </summary>
    public class WikiDataBinding
    {
        public string type { get; set; }
        public string value { get; set; }
        public string datatype { get; set; }
        public string lang { get; set; }
    }
}

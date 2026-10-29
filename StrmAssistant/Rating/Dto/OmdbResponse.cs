using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StrmAssistant.Rating.Dto
{
    /// <summary>
    /// Minimal OMDb response fields used by rating aggregation. Property casing matches the API.
    /// </summary>
    public class OmdbResponse
    {
        public string imdbRating { get; set; }
        public List<OmdbRating> Ratings { get; set; }

        public float? GetRottenTomatoScore()
        {
            var value = Ratings?.FirstOrDefault(rating => rating?.Source == "Rotten Tomatoes")?.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var score = value.Trim().TrimEnd('%').Trim();
            return float.TryParse(score, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? (float?)parsed
                : null;
        }

        public float? GetImdbScore()
        {
            return float.TryParse(imdbRating, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? (float?)parsed
                : null;
        }
    }

    public class OmdbRating
    {
        public string Source { get; set; }
        public string Value { get; set; }
    }
}

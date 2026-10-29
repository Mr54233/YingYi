using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Rating.Dto
{
    /// <summary>
    /// Minimal MDBList response fields used by rating aggregation. Names match the snake_case API.
    /// </summary>
    public class MdbListResponse
    {
        public string title { get; set; }
        public int? year { get; set; }
        public List<MdbListRating> ratings { get; set; }
        public MdbListIds ids { get; set; }

        public float? GetRottenTomatoScore()
        {
            var rating = ratings?.FirstOrDefault(value => value?.source == "rotten_tomatoes");
            return rating == null || rating.value == 0 ? (float?)null : rating.value;
        }

        public float? GetImdbScore()
        {
            var rating = ratings?.FirstOrDefault(value => value?.source == "imdb");
            return rating == null ? (float?)null : rating.value;
        }
    }

    public class MdbListRating
    {
        public string source { get; set; }
        public float value { get; set; }
    }

    public class MdbListIds
    {
        public string imdb { get; set; }
        public int? tmdb { get; set; }
        public int? trakt { get; set; }
        public int? tvdb { get; set; }
        public int? tvrage { get; set; }
        public int? letterboxd { get; set; }
        public string slug { get; set; }
    }
}

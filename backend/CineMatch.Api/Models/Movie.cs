namespace CineMatch.Api.Models
{
    public class Movie
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PosterPath { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new List<string>();
        public List<string> Actors { get; set; } = new List<string>();
        public List<string> Directors { get; set; } = new List<string>();
        public List<string> StreamingPlatforms { get; set; } = new List<string>();
    }
}

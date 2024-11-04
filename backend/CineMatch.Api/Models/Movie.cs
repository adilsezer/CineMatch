namespace CineMatch.Api.Models
{
    public class Movie
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PosterPath { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new List<string>();
        public List<Person> Actors { get; set; } = new List<Person>();
        public List<Person> Directors { get; set; } = new List<Person>();
    }
}

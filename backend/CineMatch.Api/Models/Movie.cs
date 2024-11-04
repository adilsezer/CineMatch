namespace CineMatch.Api.Models
{
    public class Movie : IEquatable<Movie>
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string PosterPath { get; set; } = "";
        public string Overview { get; set; } = "";
        public List<string> Genres { get; set; } = new List<string>();
        public List<Person> Actors { get; set; } = new List<Person>();
        public List<Person> Directors { get; set; } = new List<Person>();

        public bool Equals(Movie? other)
        {
            if (other is null)
                return false;

            return this.Id == other.Id;
        }

        public override bool Equals(object? obj) => Equals(obj as Movie);

        public override int GetHashCode() => Id.GetHashCode();
    }
}

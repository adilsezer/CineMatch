namespace CineMatch.Api.Models
{
    public class RecommendationRequest
    {
        public List<int> SelectedMovieIds { get; set; } = new List<int>();
    }
}

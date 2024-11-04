using CineMatch.Api.Models;

namespace CineMatch.Api.Services
{
    public interface IMovieService
    {
        Task<List<Movie>> SearchMoviesAsync(string query);
        Task<Movie?> GetMovieDetailsAsync(int movieId);
        Task<List<Movie>> GetRecommendationsAsync(List<int> selectedMovieIds, string? platformFilter);
    }
}

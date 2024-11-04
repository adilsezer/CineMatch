using CineMatch.Api.Models;
using CineMatch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CineMatch.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(IMovieService movieService, ILogger<MoviesController> logger)
        {
            _movieService = movieService;
            _logger = logger;
        }

        // GET: api/movies/search?query=Inception
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query cannot be empty.");

            var results = await _movieService.SearchMoviesAsync(query);
            return Ok(results);
        }

        // GET: api/movies/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovie(int id)
        {
            var movie = await _movieService.GetMovieDetailsAsync(id);
            if (movie == null)
                return NotFound();
            return Ok(movie);
        }

        // POST: api/movies/recommendations
        [HttpPost("recommendations")]
        public async Task<IActionResult> Recommend([FromBody] RecommendationRequest request)
        {
            if (request.SelectedMovieIds == null || !request.SelectedMovieIds.Any())
                return BadRequest("At least one selected movie is required.");

            var recommendations = await _movieService.GetRecommendationsAsync(request.SelectedMovieIds, request.PlatformFilter);
            return Ok(recommendations);
        }
    }
}

using CineMatch.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace CineMatch.Api.Services
{
    public class MovieService : IMovieService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IMemoryCache _cache;

        public MovieService(IConfiguration configuration, IMemoryCache cache)
        {
            // Retrieve the API key from the configuration
            _apiKey = configuration["Tmdb:ApiKey"] ?? throw new ArgumentNullException("TMDb API Key not found");
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.themoviedb.org/3/")
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _cache = cache;
        }

        public async Task<List<Movie>> SearchMoviesAsync(string query)
        {
            string cacheKey = $"Search_{query}";
            if (!_cache.TryGetValue(cacheKey, out List<Movie> movies))
            {
                var response = await _httpClient.GetAsync($"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject(json);
                    movies = new List<Movie>();
                    foreach (var item in data.results)
                    {
                        movies.Add(new Movie
                        {
                            Id = item.id,
                            Title = item.title,
                            PosterPath = item.poster_path != null ? $"https://image.tmdb.org/t/p/w500{item.poster_path}" : "",
                            Overview = item.overview
                        });
                    }
                    _cache.Set(cacheKey, movies, TimeSpan.FromMinutes(30));
                }
                else
                {
                    movies = new List<Movie>();
                }
            }
            return movies;
        }

        public async Task<Movie?> GetMovieDetailsAsync(int movieId)
        {
            string cacheKey = $"Movie_{movieId}";
            if (!_cache.TryGetValue(cacheKey, out Movie? movie))
            {
                var response = await _httpClient.GetAsync($"movie/{movieId}?api_key={_apiKey}&append_to_response=credits");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject(json);
                    movie = new Movie
                    {
                        Id = data.id,
                        Title = data.title,
                        PosterPath = data.poster_path != null ? $"https://image.tmdb.org/t/p/w500{data.poster_path}" : "",
                        Overview = data.overview,
                        Genres = ((IEnumerable<dynamic>)data.genres).Select(g => (string)g.name).ToList(),
                        Actors = ((IEnumerable<dynamic>)data.credits.cast).Take(5).Select(a => (string)a.name).ToList(),
                        Directors = ((IEnumerable<dynamic>)data.credits.crew).Where(c => (string)c.job == "Director").Select(d => (string)d.name).ToList(),
                        StreamingPlatforms = GetStreamingPlatformsMock()
                    };
                    _cache.Set(cacheKey, movie, TimeSpan.FromMinutes(60));
                }
            }
            return movie;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(List<int> selectedMovieIds, string? platformFilter)
        {
            List<Movie> recommendations = new List<Movie>();
            foreach (var id in selectedMovieIds)
            {
                var movie = await GetMovieDetailsAsync(id);
                if (movie != null)
                {
                    foreach (var genre in movie.Genres)
                    {
                        string cacheKey = $"Genre_{genre}";
                        List<Movie>? genreMovies;
                        if (!_cache.TryGetValue(cacheKey, out genreMovies))
                        {
                            var response = await _httpClient.GetAsync($"discover/movie?api_key={_apiKey}&with_genres={GetGenreId(genre)}");
                            if (response.IsSuccessStatusCode)
                            {
                                var json = await response.Content.ReadAsStringAsync();
                                dynamic data = JsonConvert.DeserializeObject(json);
                                genreMovies = new List<Movie>();
                                foreach (var item in data.results)
                                {
                                    genreMovies.Add(new Movie
                                    {
                                        Id = item.id,
                                        Title = item.title,
                                        PosterPath = item.poster_path != null ? $"https://image.tmdb.org/t/p/w500{item.poster_path}" : "",
                                        Overview = item.overview
                                    });
                                }
                                _cache.Set(cacheKey, genreMovies, TimeSpan.FromMinutes(30));
                            }
                            else
                            {
                                genreMovies = new List<Movie>();
                            }
                        }
                        recommendations.AddRange(genreMovies);
                    }
                }
            }

            recommendations = recommendations
                .Where(m => !selectedMovieIds.Contains(m.Id))
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .ToList();

            if (!string.IsNullOrEmpty(platformFilter))
            {
                recommendations = recommendations.Where(m => m.StreamingPlatforms.Contains(platformFilter)).ToList();
            }

            return recommendations.Take(20).ToList();
        }

        private List<string> GetStreamingPlatformsMock()
        {
            var platforms = new List<string> { "Netflix", "Amazon Prime", "Hulu", "Disney+", "HBO Max" };
            var random = new Random();
            int count = random.Next(1, platforms.Count);
            return platforms.OrderBy(x => random.Next()).Take(count).ToList();
        }

        private int GetGenreId(string genre)
        {
            var genreMap = new Dictionary<string, int>
            {
                {"Action", 28},
                {"Adventure", 12},
                {"Animation", 16},
                {"Comedy", 35},
                {"Crime", 80},
                {"Documentary", 99},
                {"Drama", 18},
                {"Family", 10751},
                {"Fantasy", 14},
                {"History", 36},
                {"Horror", 27},
                {"Music", 10402},
                {"Mystery", 9648},
                {"Romance", 10749},
                {"Science Fiction", 878},
                {"TV Movie", 10770},
                {"Thriller", 53},
                {"War", 10752},
                {"Western", 37}
            };
            return genreMap.ContainsKey(genre) ? genreMap[genre] : 28;
        }
    }
}

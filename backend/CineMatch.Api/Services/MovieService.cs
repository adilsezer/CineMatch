using CineMatch.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Collections.Concurrent;

namespace CineMatch.Api.Services
{
    public class MovieService : IMovieService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IMemoryCache _cache;

        public MovieService(IConfiguration configuration, IMemoryCache cache)
        {
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
                        Actors = ((IEnumerable<dynamic>)data.credits.cast)
                            .Take(5)
                            .Select(a => new Person { Id = a.id, Name = a.name })
                            .ToList(),
                        Directors = ((IEnumerable<dynamic>)data.credits.crew)
                            .Where(c => (string)c.job == "Director")
                            .Select(d => new Person { Id = d.id, Name = d.name })
                            .ToList(),
                    };
                    _cache.Set(cacheKey, movie, TimeSpan.FromMinutes(60));
                }
            }
            return movie;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(List<int> selectedMovieIds)
        {
            var recommendationScores = new ConcurrentDictionary<int, int>();
            var recommendations = new ConcurrentBag<Movie>();

            var tasks = selectedMovieIds.Select(async id =>
            {
                var movie = await GetMovieDetailsAsync(id);
                if (movie == null) return;

                // Get similar movies
                var similarMovies = await GetSimilarMovies(id, recommendationScores, 5);
                foreach (var simMovie in similarMovies)
                {
                    recommendations.Add(simMovie);
                }

                // Get movies by genres
                var genreTasks = movie.Genres.Select(async genre =>
                {
                    var genreMovies = await GetMoviesByCriteria("Genre", genre, recommendationScores, 3);
                    foreach (var genreMovie in genreMovies)
                    {
                        recommendations.Add(genreMovie);
                    }
                });

                // Get movies by actors
                var actorTasks = movie.Actors.Select(async actor =>
                {
                    var actorMovies = await GetMoviesByCriteria("Actor", actor.Id.ToString(), recommendationScores, 2);
                    foreach (var actorMovie in actorMovies)
                    {
                        recommendations.Add(actorMovie);
                    }
                });

                // Get movies by directors
                var directorTasks = movie.Directors.Select(async director =>
                {
                    var directorMovies = await GetMoviesByCriteria("Director", director.Id.ToString(), recommendationScores, 4);
                    foreach (var directorMovie in directorMovies)
                    {
                        recommendations.Add(directorMovie);
                    }
                });

                await Task.WhenAll(genreTasks.Concat(actorTasks).Concat(directorTasks));
            });

            await Task.WhenAll(tasks);

            // Filter and sort recommendations
            var uniqueRecommendations = recommendations
                .Where(m => !selectedMovieIds.Contains(m.Id))
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .OrderByDescending(m => recommendationScores.GetValueOrDefault(m.Id, 0))
                .Take(20)
                .ToList();

            return uniqueRecommendations;
        }

        private async Task<List<Movie>> GetSimilarMovies(int movieId, ConcurrentDictionary<int, int> recommendationScores, int weight)
        {
            string cacheKey = $"Similar_{movieId}";
            var endpoint = $"movie/{movieId}/similar?api_key={_apiKey}";
            List<Movie> similarMovies = await FetchAndCacheMovies(endpoint, cacheKey);

            foreach (var movie in similarMovies)
            {
                recommendationScores.AddOrUpdate(movie.Id, weight, (key, oldValue) => oldValue + weight);
            }

            return similarMovies;
        }

        private async Task<List<Movie>> GetMoviesByCriteria(string criteriaType, string criteriaValue, ConcurrentDictionary<int, int> recommendationScores, int weight)
        {
            string cacheKey = $"{criteriaType}_{criteriaValue}";
            string endpoint = BuildCriteriaQuery(criteriaType, criteriaValue);
            List<Movie> criteriaMovies = await FetchAndCacheMovies(endpoint, cacheKey);

            foreach (var movie in criteriaMovies)
            {
                recommendationScores.AddOrUpdate(movie.Id, weight, (key, oldValue) => oldValue + weight);
            }

            return criteriaMovies;
        }

        private string BuildCriteriaQuery(string criteriaType, string criteriaValue)
        {
            return criteriaType switch
            {
                "Genre" => $"discover/movie?api_key={_apiKey}&with_genres={GetGenreId(criteriaValue)}",
                "Actor" => $"discover/movie?api_key={_apiKey}&with_cast={criteriaValue}",
                "Director" => $"discover/movie?api_key={_apiKey}&with_crew={criteriaValue}",
                _ => throw new ArgumentException("Invalid criteria type")
            };
        }

        private async Task<List<Movie>> FetchAndCacheMovies(string endpoint, string cacheKey)
        {
            if (_cache.TryGetValue(cacheKey, out List<Movie> movies))
            {
                return movies;
            }

            var response = await _httpClient.GetAsync(endpoint);
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
            return movies;
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
            return genreMap.ContainsKey(genre) ? genreMap[genre] : 28; // Default to "Action" if genre not found
        }
    }
}

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
            if (!_cache.TryGetValue(cacheKey, out List<Movie>? movies) || movies == null)
            {
                var response = await _httpClient.GetAsync($"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(json);
                    movies = new List<Movie>();
                    if (data?.results != null)
                    {
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
                    }
                    _cache.Set(cacheKey, movies, TimeSpan.FromMinutes(30));
                }
                else
                {
                    movies = new List<Movie>();
                }
            }
            return movies ?? new List<Movie>();
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
                    var data = JsonConvert.DeserializeObject<dynamic>(json);
                    if (data != null)
                    {
                        movie = new Movie
                        {
                            Id = data.id,
                            Title = data.title,
                            PosterPath = data.poster_path != null ? $"https://image.tmdb.org/t/p/w500{data.poster_path}" : "",
                            Overview = data.overview,
                            Genres = data.genres != null ? ((IEnumerable<dynamic>)data.genres).Select(g => (string)g.name).ToList() : new List<string>(),
                            Actors = data.credits?.cast != null
                                ? ((IEnumerable<dynamic>)data.credits.cast)
                                    .Take(3) // Reduced number of actors to simplify
                                    .Where(a => a != null)
                                    .Select(a => new Person { Id = a.id, Name = a.name })
                                    .ToList()
                                : new List<Person>(),
                            Directors = data.credits?.crew != null
                                ? ((IEnumerable<dynamic>)data.credits.crew)
                                    .Where(c => (string)c.job == "Director" && c != null)
                                    .Select(d => new Person { Id = d.id, Name = d.name })
                                    .Take(2) // Reduced number of directors to simplify
                                    .ToList()
                                : new List<Person>(),
                        };
                        _cache.Set(cacheKey, movie, TimeSpan.FromMinutes(60));
                    }
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

                // Use TMDb's recommendations endpoint
                var tmdbRecommendations = await GetTmdbRecommendations(id, recommendationScores, weight: 5);
                foreach (var recMovie in tmdbRecommendations)
                {
                    if (recMovie != null)
                    {
                        recommendations.Add(recMovie);
                    }
                }

                // Get movies by genres (limit to 2 per genre)
                var genreTasks = movie.Genres.Select(async genre =>
                {
                    var genreMovies = await GetMoviesByCriteria("Genre", genre, recommendationScores, weight: 3, limit: 2);
                    foreach (var genreMovie in genreMovies)
                    {
                        if (genreMovie != null)
                        {
                            recommendations.Add(genreMovie);
                        }
                    }
                });

                // Get movies by top actors (limit to 1 per actor)
                var actorTasks = movie.Actors.Select(async actor =>
                {
                    var actorMovies = await GetMoviesByCriteria("Actor", actor.Id.ToString(), recommendationScores, weight: 2, limit: 1);
                    foreach (var actorMovie in actorMovies)
                    {
                        if (actorMovie != null)
                        {
                            recommendations.Add(actorMovie);
                        }
                    }
                });

                // Get movies by top directors (limit to 1 per director)
                var directorTasks = movie.Directors.Select(async director =>
                {
                    var directorMovies = await GetMoviesByCriteria("Director", director.Id.ToString(), recommendationScores, weight: 4, limit: 1);
                    foreach (var directorMovie in directorMovies)
                    {
                        if (directorMovie != null)
                        {
                            recommendations.Add(directorMovie);
                        }
                    }
                });

                await Task.WhenAll(genreTasks.Concat(actorTasks).Concat(directorTasks));
            });

            await Task.WhenAll(tasks);

            // Filter and sort recommendations
            var uniqueRecommendations = recommendations
                .Where(m => m != null && !selectedMovieIds.Contains(m.Id))
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .OrderByDescending(m => recommendationScores.GetValueOrDefault(m.Id, 0))
                .Take(20)
                .ToList();

            return uniqueRecommendations ?? new List<Movie>();
        }

        private async Task<List<Movie>> GetTmdbRecommendations(int movieId, ConcurrentDictionary<int, int> recommendationScores, int weight)
        {
            string cacheKey = $"TmdbRecommendations_{movieId}";
            List<Movie>? tmdbRecommendations;

            if (!_cache.TryGetValue(cacheKey, out tmdbRecommendations) || tmdbRecommendations == null)
            {
                var endpoint = $"movie/{movieId}/recommendations?api_key={_apiKey}&language=en-US&page=1";
                tmdbRecommendations = await FetchAndCacheMovies(endpoint, cacheKey, limit: 3); // Limit to 3 recommendations
                _cache.Set(cacheKey, tmdbRecommendations, TimeSpan.FromMinutes(60));
            }

            if (tmdbRecommendations != null)
            {
                foreach (var movie in tmdbRecommendations)
                {
                    if (movie != null)
                    {
                        recommendationScores.AddOrUpdate(movie.Id, weight, (key, oldValue) => oldValue + weight);
                    }
                }
            }

            return tmdbRecommendations ?? new List<Movie>();
        }

        private async Task<List<Movie>> GetMoviesByCriteria(string criteriaType, string criteriaValue, ConcurrentDictionary<int, int> recommendationScores, int weight, int limit = 3)
        {
            List<Movie> criteriaMovies = new List<Movie>();

            if (criteriaType == "Director")
            {
                var directorMovies = await GetMoviesByDirectorAsync(criteriaValue, recommendationScores, weight, limit);
                if (directorMovies != null)
                {
                    criteriaMovies.AddRange(directorMovies);
                }
            }
            else
            {
                string cacheKey = $"{criteriaType}_{criteriaValue}";
                string endpoint = BuildCriteriaQuery(criteriaType, criteriaValue);
                var fetchedMovies = await FetchAndCacheMovies(endpoint, cacheKey, limit);
                if (fetchedMovies != null)
                {
                    criteriaMovies.AddRange(fetchedMovies);
                    foreach (var movie in fetchedMovies)
                    {
                        if (movie != null)
                        {
                            recommendationScores.AddOrUpdate(movie.Id, weight, (key, oldValue) => oldValue + weight);
                        }
                    }
                }
            }

            return criteriaMovies;
        }

        private string BuildCriteriaQuery(string criteriaType, string criteriaValue)
        {
            return criteriaType switch
            {
                "Genre" => $"discover/movie?api_key={_apiKey}&with_genres={GetGenreId(criteriaValue)}&sort_by=popularity.desc",
                "Actor" => $"discover/movie?api_key={_apiKey}&with_cast={criteriaValue}&sort_by=popularity.desc",
                // Removed "Director" case as it's handled separately
                _ => throw new ArgumentException("Invalid criteria type")
            };
        }

        private async Task<List<Movie>> GetMoviesByDirectorAsync(string directorId, ConcurrentDictionary<int, int> recommendationScores, int weight, int limit = 2)
        {
            string cacheKey = $"Director_{directorId}_Movies";
            List<Movie>? directorMovies;

            if (!_cache.TryGetValue(cacheKey, out directorMovies) || directorMovies == null)
            {
                var endpoint = $"person/{directorId}/movie_credits?api_key={_apiKey}";
                var response = await _httpClient.GetAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<dynamic>(json);
                    if (data?.crew != null)
                    {
                        directorMovies = new List<Movie>();
                        foreach (var item in data.crew)
                        {
                            if ((string)item.job == "Director")
                            {
                                directorMovies.Add(new Movie
                                {
                                    Id = item.id,
                                    Title = item.title,
                                    PosterPath = item.poster_path != null ? $"https://image.tmdb.org/t/p/w500{item.poster_path}" : "",
                                    Overview = item.overview
                                });

                                if (directorMovies.Count >= limit)
                                    break; // Limit reached
                            }
                        }
                        _cache.Set(cacheKey, directorMovies, TimeSpan.FromMinutes(60));
                    }
                    else
                    {
                        directorMovies = new List<Movie>();
                    }
                }
                else
                {
                    directorMovies = new List<Movie>();
                }
            }

            var limitedDirectorMovies = directorMovies?.Take(limit).ToList() ?? new List<Movie>();
            foreach (var movie in limitedDirectorMovies)
            {
                if (movie != null)
                {
                    recommendationScores.AddOrUpdate(movie.Id, weight, (key, oldValue) => oldValue + weight);
                }
            }

            return limitedDirectorMovies;
        }

        private async Task<List<Movie>> FetchAndCacheMovies(string endpoint, string cacheKey, int limit)
        {
            if (_cache.TryGetValue(cacheKey, out List<Movie>? movies) && movies != null)
            {
                return movies.Take(limit).ToList();
            }

            var response = await _httpClient.GetAsync(endpoint);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(json);
                movies = new List<Movie>();
                if (data?.results != null)
                {
                    foreach (var item in data.results)
                    {
                        movies.Add(new Movie
                        {
                            Id = item.id,
                            Title = item.title,
                            PosterPath = item.poster_path != null ? $"https://image.tmdb.org/t/p/w500{item.poster_path}" : "",
                            Overview = item.overview
                        });

                        if (movies.Count >= limit)
                            break; // Limit reached
                    }
                }
                _cache.Set(cacheKey, movies, TimeSpan.FromMinutes(30));
            }
            else
            {
                movies = new List<Movie>();
            }
            return movies?.Take(limit).ToList() ?? new List<Movie>();
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

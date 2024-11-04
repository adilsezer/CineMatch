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
                    Console.WriteLine($"[Warning] Failed to search movies for query: '{query}'. Status Code: {response.StatusCode}");
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
                            Genres = data.genres != null
                                ? ((IEnumerable<dynamic>)data.genres).Select(g => (string)g.name).ToList()
                                : new List<string>(),
                            Actors = data.credits?.cast != null
                                ? ((IEnumerable<dynamic>)data.credits.cast)
                                    .Take(1) // Limit to main actor/actress
                                    .Where(a => a != null)
                                    .Select(a => new Person { Id = a.id, Name = a.name })
                                    .ToList()
                                : new List<Person>(),
                            Directors = data.credits?.crew != null
                                ? ((IEnumerable<dynamic>)data.credits.crew)
                                    .Where(c => (string)c.job == "Director" && c != null)
                                    .Select(d => new Person { Id = d.id, Name = d.name })
                                    .Take(1) // Limit to top director
                                    .ToList()
                                : new List<Person>(),
                        };
                        _cache.Set(cacheKey, movie, TimeSpan.FromMinutes(60));
                    }
                }
                else
                {
                    Console.WriteLine($"[Warning] Failed to get details for movie ID: {movieId}. Status Code: {response.StatusCode}");
                }
            }
            return movie;
        }

        public async Task<List<Movie>> GetRecommendationsAsync(List<int> selectedMovieIds)
        {
            // Initialize a thread-safe collection to store all recommendations
            var allSimilarRecommendations = new ConcurrentBag<Movie>();
            var allActorRecommendations = new ConcurrentBag<Movie>();
            var allDirectorRecommendations = new ConcurrentBag<Movie>();

            // Process each selected movie independently
            var tasks = selectedMovieIds.Select(async id =>
            {
                var movie = await GetMovieDetailsAsync(id);
                if (movie == null)
                {
                    Console.WriteLine($"[Warning] Movie details not found for ID: {id}. Skipping recommendations for this movie.");
                    return;
                }

                // 1. Fetch Top 2 Similar Movies
                var tmdbRecommendations = await GetTmdbRecommendations(id, limit: 2);
                foreach (var recMovie in tmdbRecommendations)
                {
                    if (recMovie != null && !selectedMovieIds.Contains(recMovie.Id))
                    {
                        allSimilarRecommendations.Add(recMovie);
                        Console.WriteLine($"[Info] Recommended (Similar): '{recMovie.Title}' for selected movie ID: {id}");
                    }
                }

                // 2. Fetch 1 Movie Based on Main Actor/Actress
                if (movie.Actors != null && movie.Actors.Any())
                {
                    var mainActor = movie.Actors.First();
                    var actorMovies = await GetMoviesByCriteria("Actor", mainActor.Id.ToString(), limit: 5); // Increased limit to find unique
                    foreach (var actorMovie in actorMovies)
                    {
                        if (actorMovie != null && !selectedMovieIds.Contains(actorMovie.Id) && !allSimilarRecommendations.Contains(actorMovie))
                        {
                            allActorRecommendations.Add(actorMovie);
                            Console.WriteLine($"[Info] Recommended (Actor: {mainActor.Name}): '{actorMovie.Title}' for selected movie ID: {id}");
                            break; // Only add one recommendation
                        }
                        else if (actorMovie != null && selectedMovieIds.Contains(actorMovie.Id))
                        {
                            Console.WriteLine($"[Info] Skipping duplicate recommendation (Actor-Based): '{actorMovie.Title}' for selected movie ID: {id}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[Warning] No actors found for movie ID: {id}. Skipping actor-based recommendation.");
                }

                // 3. Fetch 1 Movie Based on Director
                if (movie.Directors != null && movie.Directors.Any())
                {
                    var topDirector = movie.Directors.First(); // Consider the first director
                    var directorMovies = await GetMoviesByDirectorAsync(topDirector.Id.ToString(), limit: 5); // Increased limit to find unique
                    foreach (var directorMovie in directorMovies)
                    {
                        if (directorMovie != null && !selectedMovieIds.Contains(directorMovie.Id) && !allSimilarRecommendations.Contains(directorMovie))
                        {
                            allDirectorRecommendations.Add(directorMovie);
                            Console.WriteLine($"[Info] Recommended (Director: {topDirector.Name}): '{directorMovie.Title}' for selected movie ID: {id}");
                            break; // Only add one recommendation
                        }
                        else if (directorMovie != null && selectedMovieIds.Contains(directorMovie.Id))
                        {
                            Console.WriteLine($"[Info] Skipping duplicate recommendation (Director-Based): '{directorMovie.Title}' for selected movie ID: {id}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[Warning] No directors found for movie ID: {id}. Skipping director-based recommendation.");
                }
            });

            await Task.WhenAll(tasks);

            // Aggregate Recommendations
            var finalRecommendations = new List<Movie>();

            // 1. Add Top 2 Similar Movies
            finalRecommendations.AddRange(allSimilarRecommendations.Take(2));

            // 2. Add 1 Actor-Based Movie
            finalRecommendations.AddRange(allActorRecommendations.Take(1));

            // 3. Add 1 Director-Based Movie
            finalRecommendations.AddRange(allDirectorRecommendations.Take(1));

            // Ensure there are no duplicates
            finalRecommendations = finalRecommendations
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"[Info] Total recommendations generated: {finalRecommendations.Count}");

            return finalRecommendations ?? new List<Movie>();
        }

        private async Task<List<Movie>> GetTmdbRecommendations(int movieId, int limit)
        {
            string cacheKey = $"TmdbRecommendations_{movieId}";
            List<Movie>? tmdbRecommendations;

            if (!_cache.TryGetValue(cacheKey, out tmdbRecommendations) || tmdbRecommendations == null)
            {
                var endpoint = $"movie/{movieId}/recommendations?api_key={_apiKey}&language=en-US&page=1";
                tmdbRecommendations = await FetchAndCacheMovies(endpoint, cacheKey, limit);
                _cache.Set(cacheKey, tmdbRecommendations, TimeSpan.FromMinutes(60));
            }

            return tmdbRecommendations ?? new List<Movie>();
        }

        private async Task<List<Movie>> GetMoviesByCriteria(string criteriaType, string criteriaValue, int limit)
        {
            List<Movie> criteriaMovies = new List<Movie>();

            if (criteriaType == "Actor")
            {
                string cacheKey = $"{criteriaType}_{criteriaValue}";
                string endpoint = BuildCriteriaQuery(criteriaType, criteriaValue);
                var fetchedMovies = await FetchAndCacheMovies(endpoint, cacheKey, limit);
                if (fetchedMovies != null)
                {
                    criteriaMovies.AddRange(fetchedMovies);
                }
            }

            return criteriaMovies;
        }

        private string BuildCriteriaQuery(string criteriaType, string criteriaValue)
        {
            return criteriaType switch
            {
                "Actor" => $"discover/movie?api_key={_apiKey}&with_cast={criteriaValue}&sort_by=popularity.desc",
                _ => throw new ArgumentException("Invalid criteria type")
            };
        }

        private async Task<List<Movie>> GetMoviesByDirectorAsync(string directorId, int limit)
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
                    Console.WriteLine($"[Warning] Failed to get movie credits for director ID: {directorId}. Status Code: {response.StatusCode}");
                    directorMovies = new List<Movie>();
                }
            }

            var limitedDirectorMovies = directorMovies?.Take(limit).ToList() ?? new List<Movie>();
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
                Console.WriteLine($"[Warning] Failed to fetch movies from endpoint: '{endpoint}'. Status Code: {response.StatusCode}");
                movies = new List<Movie>();
            }
            return movies?.Take(limit).ToList() ?? new List<Movie>();
        }

        // Removed GetGenreId method as it's no longer needed
    }
}

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
        private readonly Random _random;

        public MovieService(IConfiguration configuration, IMemoryCache cache)
        {
            _apiKey = configuration["Tmdb:ApiKey"] ?? throw new ArgumentNullException("TMDb API Key not found");
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.themoviedb.org/3/")
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _cache = cache;
            _random = new Random();
        }

        /// <summary>
        /// Searches for movies based on a query string.
        /// </summary>
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

        /// <summary>
        /// Retrieves detailed information about a specific movie.
        /// </summary>
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
                                    .Take(5) // Fetch top 5 actors/actresses
                                    .Where(a => a != null)
                                    .Select(a => new Person { Id = a.id, Name = a.name })
                                    .ToList()
                                : new List<Person>(),
                            Directors = data.credits?.crew != null
                                ? ((IEnumerable<dynamic>)data.credits.crew)
                                    .Where(c => (string)c.job == "Director" && c != null)
                                    .Select(d => new Person { Id = d.id, Name = d.name })
                                    .Take(2) // Fetch top 2 directors
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

        /// <summary>
        /// Generates a list of movie recommendations based on selected movie IDs.
        /// Each recommendation type (similar, actor-based, director-based) is based on a distinct randomly selected movie.
        /// </summary>
        public async Task<List<Movie>> GetRecommendationsAsync(List<int> selectedMovieIds)
        {
            if (selectedMovieIds == null || !selectedMovieIds.Any())
            {
                Console.WriteLine("[Warning] No selected movies provided for recommendations.");
                return new List<Movie>();
            }

            // Shuffle the selectedMovieIds to ensure randomness
            var shuffledMovieIds = selectedMovieIds.OrderBy(x => _random.Next()).ToList();

            // Assign different movies for each recommendation type
            // Ensure that there are enough movies; if not, reuse the available ones
            int recommendationTypes = 3; // Similar, Actor-Based, Director-Based
            var selectedForRecommendations = new List<int>();

            for (int i = 0; i < recommendationTypes && i < shuffledMovieIds.Count; i++)
            {
                selectedForRecommendations.Add(shuffledMovieIds[i]);
            }

            // If there are fewer selected movies than recommendation types, allow reuse
            while (selectedForRecommendations.Count < recommendationTypes)
            {
                selectedForRecommendations.Add(shuffledMovieIds[_random.Next(shuffledMovieIds.Count)]);
            }

            // Extract distinct movie IDs for each recommendation type
            // similarMovieId: selectedForRecommendations[0]
            // actorMovieId: selectedForRecommendations[1]
            // directorMovieId: selectedForRecommendations[2]
            int similarMovieId = selectedForRecommendations[0];
            int actorMovieId = selectedForRecommendations[1];
            int directorMovieId = selectedForRecommendations[2];

            // Initialize collections for different recommendation types
            var similarRecommendations = new ConcurrentBag<Movie>();
            var actorRecommendations = new ConcurrentBag<Movie>();
            var directorRecommendations = new ConcurrentBag<Movie>();

            // Process Similar Recommendations
            var similarMovie = await GetMovieDetailsAsync(similarMovieId);
            if (similarMovie != null)
            {
                var tmdbSimilar = await GetTmdbRecommendations(similarMovieId, limit: 2);
                foreach (var rec in tmdbSimilar)
                {
                    if (rec != null && !selectedMovieIds.Contains(rec.Id))
                    {
                        similarRecommendations.Add(rec);
                        Console.WriteLine($"[Info] Similar Recommendation: '{rec.Title}' based on '{similarMovie.Title}'");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[Warning] Similar recommendations: Movie ID {similarMovieId} details not found.");
            }

            // Process Actor-Based Recommendations
            var actorMovie = await GetMovieDetailsAsync(actorMovieId);
            if (actorMovie != null && actorMovie.Actors.Any())
            {
                var mainActor = actorMovie.Actors.First();
                var actorBasedMovies = await GetMoviesByCriteria("Actor", mainActor.Id.ToString(), limit: 10); // Increased limit to find unique
                var uniqueActorMovie = actorBasedMovies.FirstOrDefault(m => m != null && !selectedMovieIds.Contains(m.Id) && !similarRecommendations.Contains(m));
                if (uniqueActorMovie != null)
                {
                    actorRecommendations.Add(uniqueActorMovie);
                    Console.WriteLine($"[Info] Actor-Based Recommendation: '{uniqueActorMovie.Title}' based on actor '{mainActor.Name}' from '{actorMovie.Title}'");
                }
                else
                {
                    Console.WriteLine($"[Warning] No suitable actor-based recommendation found for actor '{mainActor.Name}' from '{actorMovie.Title}'.");
                }
            }
            else
            {
                Console.WriteLine($"[Warning] Actor-based recommendations: Movie ID {actorMovieId} has no actors.");
            }

            // Process Director-Based Recommendations
            var directorMovie = await GetMovieDetailsAsync(directorMovieId);
            if (directorMovie != null && directorMovie.Directors.Any())
            {
                var topDirector = directorMovie.Directors.First();
                var directorBasedMovies = await GetMoviesByDirectorAsync(topDirector.Id.ToString(), limit: 10); // Increased limit to find unique
                var uniqueDirectorMovie = directorBasedMovies.FirstOrDefault(m => m != null && !selectedMovieIds.Contains(m.Id) && !similarRecommendations.Contains(m) && !actorRecommendations.Contains(m));
                if (uniqueDirectorMovie != null)
                {
                    directorRecommendations.Add(uniqueDirectorMovie);
                    Console.WriteLine($"[Info] Director-Based Recommendation: '{uniqueDirectorMovie.Title}' based on director '{topDirector.Name}' from '{directorMovie.Title}'");
                }
                else
                {
                    Console.WriteLine($"[Warning] No suitable director-based recommendation found for director '{topDirector.Name}' from '{directorMovie.Title}'.");
                }
            }
            else
            {
                Console.WriteLine($"[Warning] Director-based recommendations: Movie ID {directorMovieId} has no directors.");
            }

            // Aggregate all recommendations
            var finalRecommendations = new List<Movie>();

            finalRecommendations.AddRange(similarRecommendations.Take(2));
            finalRecommendations.AddRange(actorRecommendations.Take(1));
            finalRecommendations.AddRange(directorRecommendations.Take(1));

            // Remove duplicates if any (though should be none due to prior checks)
            finalRecommendations = finalRecommendations
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"[Info] Total recommendations generated: {finalRecommendations.Count}");

            return finalRecommendations;
        }

        /// <summary>
        /// Retrieves similar movie recommendations from TMDb.
        /// </summary>
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

        /// <summary>
        /// Retrieves movies based on specific criteria, such as actor.
        /// </summary>
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

        /// <summary>
        /// Builds the API query string based on the criteria type and value.
        /// </summary>
        private string BuildCriteriaQuery(string criteriaType, string criteriaValue)
        {
            return criteriaType switch
            {
                "Actor" => $"discover/movie?api_key={_apiKey}&with_cast={criteriaValue}&sort_by=popularity.desc",
                _ => throw new ArgumentException("Invalid criteria type")
            };
        }

        /// <summary>
        /// Retrieves movies directed by a specific director.
        /// </summary>
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

        /// <summary>
        /// Fetches movies from a specific API endpoint and caches the results.
        /// </summary>
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
    }
}

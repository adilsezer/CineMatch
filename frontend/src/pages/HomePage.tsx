/* src/pages/HomePage.tsx */
import React, { useState } from "react";
import SearchBar from "../components/SearchBar";
import MovieCard from "../components/MovieCard";
import axios from "axios";

interface Movie {
  id: number;
  title: string;
  posterPath: string;
  overview: string;
  genres?: string[];
  actors?: string[];
  directors?: string[];
}

const HomePage: React.FC = () => {
  const [searchResults, setSearchResults] = useState<Movie[]>([]);
  const [selectedMovies, setSelectedMovies] = useState<Movie[]>([]);
  const [recommendations, setRecommendations] = useState<Movie[]>([]);
  const [searchQuery, setSearchQuery] = useState<string>("");

  const handleSearch = async (query: string) => {
    try {
      const response = await axios.get<Movie[]>(
        `${process.env.REACT_APP_API_URL}/api/movies/search?query=${query}`
      );
      setSearchResults(response.data);
      setRecommendations([]);
    } catch (error) {
      console.error("Error searching movies:", error);
    }
  };

  const handleSelectMovie = async (movie: Movie) => {
    if (
      selectedMovies.length < 5 &&
      !selectedMovies.find((m) => m.id === movie.id)
    ) {
      try {
        const response = await axios.get<Movie>(
          `${process.env.REACT_APP_API_URL}/api/movies/${movie.id}`
        );
        setSelectedMovies([...selectedMovies, response.data]);
      } catch (error) {
        console.error("Error fetching movie details:", error);
      }
    } else if (selectedMovies.length >= 5) {
      alert("You can only select up to 5 movies.");
    }
  };

  const handleRemoveSelectedMovie = (movieId: number) => {
    setSelectedMovies(selectedMovies.filter((m) => m.id !== movieId));
  };

  const handleGetRecommendations = async () => {
    try {
      const request = {
        selectedMovieIds: selectedMovies.map((m) => m.id),
      };
      const response = await axios.post<Movie[]>(
        `${process.env.REACT_APP_API_URL}/api/movies/recommendations`,
        request
      );
      setRecommendations(response.data.slice(0, 4));
      setSearchResults([]);
      setSelectedMovies([]);
      setSearchQuery("");
    } catch (error) {
      console.error("Error getting recommendations:", error);
    }
  };

  const handleSearchAgain = () => {
    setRecommendations([]);
    setSearchResults([]);
    setSelectedMovies([]);
    setSearchQuery("");
  };

  return (
    <div className="bg-background min-h-screen flex flex-col">
      <div className="flex-grow">
        {recommendations.length === 0 ? (
          <div>
            <div className="p-6 text-center">
              <p className="text-text text-lg mb-6">
                Discover movies that match your taste! Select up to 5 favorites
                below, and receive personalized recommendations.
              </p>
            </div>
            <SearchBar
              onSearch={handleSearch}
              value={searchQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                setSearchQuery(e.target.value)
              }
            />
          </div>
        ) : (
          <div className="flex justify-center m-6">
            <button
              onClick={handleSearchAgain}
              className="bg-primary hover:bg-primary-dark text-white px-6 py-3 rounded-md shadow transition-colors duration-300 mb-6"
            >
              Search Again
            </button>
          </div>
        )}

        {selectedMovies.length > 0 && (
          <div className="bg-surface p-4 shadow-md flex flex-col items-center sticky top-0 z-10 rounded-lg">
            <div className="flex space-x-4 overflow-x-auto justify-center">
              {selectedMovies.map((movie) => (
                <div key={movie.id} className="relative flex-shrink-0 mx-auto">
                  <img
                    src={movie.posterPath}
                    alt={movie.title}
                    className="w-24 h-36 object-cover rounded-lg"
                  />
                  <button
                    onClick={() => handleRemoveSelectedMovie(movie.id)}
                    className="remove-button"
                    aria-label={`Remove ${movie.title}`}
                  >
                    &times;
                  </button>
                </div>
              ))}
            </div>
            <button
              onClick={handleGetRecommendations}
              className="bg-secondary hover:bg-secondary-dark text-white px-4 py-2 rounded-md shadow transition-colors duration-300 mt-4"
            >
              Get Recommendations
            </button>
          </div>
        )}

        {recommendations.length > 0 ? (
          <div className="my-8">
            <h2 className="text-2xl font-semibold mb-4 text-text">
              Recommendations
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
              {recommendations.map((movie) => (
                <MovieCard key={movie.id} movie={movie} />
              ))}
            </div>
          </div>
        ) : searchResults.length > 0 ? (
          <div className="my-8">
            <h2 className="text-2xl font-semibold mb-4 text-text">
              Search Results
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
              {searchResults.map((movie) => (
                <div
                  key={movie.id}
                  onClick={() => handleSelectMovie(movie)}
                  className="cursor-pointer"
                >
                  <MovieCard movie={movie} />
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
};

export default HomePage;

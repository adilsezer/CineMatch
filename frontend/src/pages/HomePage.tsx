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

  const handleSearch = async (query: string) => {
    try {
      const response = await axios.get<Movie[]>(
        `https://localhost:7049/api/movies/search?query=${query}`
      );
      setSearchResults(response.data);
    } catch (error) {
      console.error("Error searching movies:", error);
    }
  };

  const handleSelectMovie = async (movie: Movie) => {
    if (!selectedMovies.find((m) => m.id === movie.id)) {
      try {
        const response = await axios.get<Movie>(
          `https://localhost:7049/api/movies/${movie.id}`
        );
        setSelectedMovies([...selectedMovies, response.data]);
      } catch (error) {
        console.error("Error fetching movie details:", error);
      }
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
        `https://localhost:7049/api/movies/recommendations`,
        request
      );
      setRecommendations(response.data);
    } catch (error) {
      console.error("Error getting recommendations:", error);
    }
  };

  return (
    <div className="container mx-auto p-6 max-w-7xl">
      <SearchBar onSearch={handleSearch} />
      <hr className="my-6" />

      {/* Display search results */}
      {searchResults.length > 0 && (
        <>
          <h2 className="text-lg font-semibold mb-4">Search Results</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6 justify-items-center">
            {searchResults.map((movie) => (
              <MovieCard key={movie.id} movie={movie} />
            ))}
          </div>
          <hr className="my-6" />
        </>
      )}

      {/* Display selected movies */}
      <h2 className="text-lg font-semibold mb-4">Selected Movies</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6 justify-items-center">
        {selectedMovies.map((movie) => (
          <MovieCard
            key={movie.id}
            movie={movie}
            onRemove={() => handleRemoveSelectedMovie(movie.id)}
          />
        ))}
      </div>
    </div>
  );
};

export default HomePage;

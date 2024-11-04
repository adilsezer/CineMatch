/* src/components/MovieCard.tsx */
import React from "react";

interface Movie {
  id: number;
  title: string;
  posterPath: string;
  overview: string;
}

interface MovieCardProps {
  movie: Movie;
  onRemove?: (id: number) => void;
}

const MovieCard: React.FC<MovieCardProps> = ({ movie, onRemove }) => {
  return (
    <div className="flex flex-col h-full bg-surface rounded-lg overflow-hidden shadow-md transition-transform transform hover:scale-105">
      {movie.posterPath && (
        <img
          src={movie.posterPath}
          alt={movie.title}
          className="w-full h-64 object-cover"
        />
      )}
      <div className="flex flex-col flex-grow p-4">
        <h2 className="text-xl font-semibold mb-2 text-text">{movie.title}</h2>
        <p className="text-muted text-sm line-clamp-3">
          {movie.overview.length > 100
            ? `${movie.overview.substring(0, 100)}...`
            : movie.overview}
        </p>
      </div>
      {onRemove && (
        <div className="p-4 flex justify-end">
          <button
            onClick={() => onRemove(movie.id)}
            className="text-red-500 hover:text-red-600"
            aria-label="Remove Movie"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              className="w-6 h-6"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>
      )}
    </div>
  );
};

export default MovieCard;

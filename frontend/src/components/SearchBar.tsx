import React, { useState } from "react";

interface SearchBarProps {
  onSearch: (query: string) => void;
}

const SearchBar: React.FC<SearchBarProps> = ({ onSearch }) => {
  const [query, setQuery] = useState<string>("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (query.trim()) {
      onSearch(query.trim());
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="search-bar-container flex items-center space-x-2"
    >
      <input
        type="text"
        placeholder="Search Movies"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        className="search-input border border-gray-300 p-2 rounded w-full"
      />
      <button
        type="submit"
        className="search-button bg-blue-500 text-white p-2 rounded"
      >
        Search
      </button>
    </form>
  );
};

export default SearchBar;

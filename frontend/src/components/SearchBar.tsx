/* src/components/SearchBar.tsx */
import React from "react";

interface SearchBarProps {
  onSearch: (query: string) => void;
  value: string;
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
}

const SearchBar: React.FC<SearchBarProps> = ({ onSearch, value, onChange }) => {
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (value.trim()) {
      onSearch(value.trim());
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="flex items-center space-x-4 w-full mb-6"
    >
      <input
        type="text"
        placeholder="Search Movies"
        value={value}
        onChange={onChange}
        className="flex-1 bg-surface text-text border border-muted p-4 rounded-md focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary text-lg"
      />
      <button
        type="submit"
        className="bg-primary hover:bg-primary-dark text-white px-6 py-3 rounded-md transition-colors duration-300 text-lg"
      >
        Search
      </button>
    </form>
  );
};

export default SearchBar;

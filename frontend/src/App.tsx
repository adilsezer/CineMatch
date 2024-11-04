/* src/App.tsx */
import React from "react";
import HomePage from "./pages/HomePage";

function App() {
  return (
    <div className="bg-background min-h-screen flex flex-col items-center">
      <div className="max-w-5xl w-full px-4">
        <h1 className="text-3xl font-bold text-center my-4 text-text">
          CineMatch
        </h1>
        <HomePage />
      </div>
    </div>
  );
}

export default App;

// App.jsx
import React from "react";
import HomePage from "./pages/HomePage";

function App() {
  return (
    <div className="bg-background min-h-screen flex flex-col items-center">
      <div className="max-w-screen-lg w-full px-4">
        <h1 className="text-3xl font-bold text-center my-6">CineMatch</h1>
        <HomePage />
      </div>
    </div>
  );
}

export default App;

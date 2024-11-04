/* src/App.tsx */
import React from "react";
import HomePage from "./pages/HomePage";

function App() {
  return (
    <div className="bg-background min-h-screen flex flex-col items-center justify-center p-6">
      <div className="max-w-5xl w-full px-4 flex flex-col items-center">
        <img
          src={`${process.env.PUBLIC_URL}/logo-cinematch.png`}
          alt="Cinematch Logo"
          style={{
            width: "175px",
            height: "175px",
          }}
        />
        <p className="text-3xl font-bold text-center text-text mt-6">
          CineMatch
        </p>
        <HomePage />
      </div>
    </div>
  );
}

export default App;

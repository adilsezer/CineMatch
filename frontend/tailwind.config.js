// tailwind.config.js
module.exports = {
  content: ["./src/**/*.{js,jsx,ts,tsx}", "./public/index.html"],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: "#14B8A6",
          dark: "#0D9488",
          light: "#2DD4BF",
        },
        secondary: {
          DEFAULT: "#F87171",
          dark: "#DC2626",
          light: "#FCA5A5",
        },
        accent: {
          DEFAULT: "#FBBF24",
          dark: "#F59E0B",
          light: "#FCD34D",
        },
        background: {
          DEFAULT: "#F3F4F6",
          dark: "#E5E7EB",
        },
        surface: {
          DEFAULT: "#FFFFFF",
          dark: "#F9FAFB",
        },
        text: {
          DEFAULT: "#111827",
          muted: "#6B7280",
        },
        muted: {
          DEFAULT: "#6B7280",
        },
      },
      fontFamily: {
        sans: ["Inter", "sans-serif"],
        serif: ["Merriweather", "serif"],
      },
      fontSize: {
        base: "1rem",
        lg: "1.125rem",
        xl: "1.25rem",
        "2xl": "1.5rem",
        "3xl": "1.875rem",
      },
    },
  },
  plugins: [require("@tailwindcss/line-clamp")],
};

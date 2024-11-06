# CineMatch Backend

This is the .NET Core backend API for CineMatch, a movie recommendation web application.

## Setup

### Environment Variables

Create a `.env` file in the `backend/CineMatch.Api` folder with the following:

```plaintext
TMDB_API_KEY=<your_tmdb_api_key>
```

Replace `<your_tmdb_api_key>` with your API key for The Movie Database (TMDB).

### Local Development

1. **Navigate** to the backend folder:

   ```bash
   cd backend/CineMatch.Api
   ```

2. **Run the application** in development mode:
   ```bash
   dotnet run
   ```

The API will be available at `https://localhost:7049`.

## Deployment

The backend is set up for deployment on Azure Web Apps using a GitHub Actions workflow (`backend-deploy.yml`) in the `.github/workflows` directory.

## Learn More

- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [TMDB API Documentation](https://developers.themoviedb.org/3)

---

[GitHub Repository](https://github.com/adilsezer/CineMatch)

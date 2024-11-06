# CineMatch

CineMatch is a web app for personalized movie recommendations, using a React frontend and .NET backend.

## Project Structure

- **Frontend**: React, `frontend` folder
- **Backend**: ASP.NET Core, `backend/CineMatch.Api` folder

## Deployment

### Vercel (Frontend)

1. Set `REACT_APP_API_URL` in Vercel to point to your Azure API URL:
   - Go to **Settings** > **Environment Variables** in Vercel.
   - Add:
     - **Key**: `REACT_APP_API_URL`
     - **Value**: `<your-azure-api-url>`

### Azure (Backend)

1. Deploy the backend to Azure Web Apps using the GitHub Actions workflow (`backend-deploy.yml`) in `.github/workflows`.

## Local Setup

### Requirements

- **Frontend**: Node.js, npm
- **Backend**: .NET SDK 8.0

### Run Frontend

1. Go to `frontend/` folder and create `.env`:
   ```plaintext
   REACT_APP_API_URL=<your-azure-api-url>
   ```
2. Install dependencies and start:
   ```bash
   npm install
   npm start
   ```

### Run Backend

1. Go to `backend/CineMatch.Api/` folder and create `.env`:
   ```plaintext
   TMDB_API_KEY=<your_tmdb_api_key>
   ```
2. Run backend:
   ```bash
   dotnet run
   ```

## Learn More

- [React Docs](https://reactjs.org/)
- [ASP.NET Core Docs](https://docs.microsoft.com/en-us/aspnet/core/)
- [Azure Web Apps](https://azure.microsoft.com/en-us/services/app-service/web/)
- [Vercel Docs](https://vercel.com/docs)

---

[CineMatch on GitHub](https://github.com/adilsezer/CineMatch)

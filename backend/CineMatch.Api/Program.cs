using CineMatch.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using DotNetEnv; // Ensure DotNetEnv is loaded

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
Env.Load();  // Load from .env file
var tmdbApiKey = Environment.GetEnvironmentVariable("TMDB_API_KEY");

// Add the API key to configuration if set
builder.Configuration["Tmdb:ApiKey"] = tmdbApiKey ?? throw new InvalidOperationException("TMDB_API_KEY environment variable is not set.");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IMovieService, MovieService>();

// Configure CORS to allow frontend requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

// Configure Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();

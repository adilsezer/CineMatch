using CineMatch.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
var tmdbApiKey = Environment.GetEnvironmentVariable("TMDB_API_KEY");

builder.Configuration["Tmdb:ApiKey"] = tmdbApiKey ?? throw new InvalidOperationException("TMDB_API_KEY environment variable is not set.");

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IMovieService, MovieService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder.WithOrigins("https://cine-match-rho.vercel.app")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

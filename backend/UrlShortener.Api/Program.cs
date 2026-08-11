using UrlShortener.Api.DTOs;
using UrlShortener.Api.Handlers;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IBase62Service, Base62Service>();
builder.Services.AddScoped<IUrlRepository, UrlRepository>();
builder.Services.AddScoped<IUrlService, UrlService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IUrlRepository>().EnsureUrlsTableExists();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Skipped in Development so the gul.fy hosts-file redirect can be tested over plain HTTP.
    app.UseHttpsRedirection();
}

app.UseCors();

var api = app.MapGroup("/api");
api.MapGet("/health", HealthHandler.GetHealth)
    .WithName("GetHealth");

var urls = api.MapGroup("/urls");

urls.MapPost("/", UrlHandler.CreateUrl)
    .WithName("CreateUrl");
urls.MapGet("/", UrlHandler.ListUrls);
urls.MapGet("/{code}", UrlHandler.GetUrl);
urls.MapPut("/{code}", UrlHandler.UpdateUrl);
urls.MapDelete("/{code}", UrlHandler.DeleteUrl);

// Redirect endpoint lives at the root so short links look like gul.fy/{code}, not gul.fy/api/urls/{code}.
app.MapGet("/{code}", UrlHandler.RedirectToDestination);

app.Run();

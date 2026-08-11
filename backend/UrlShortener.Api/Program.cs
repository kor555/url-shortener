using UrlShortener.Api.DTOs;
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
api.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "UrlShortener.Api"
}))
.WithName("GetHealth");

var urls = api.MapGroup("/urls");

urls.MapPost("/", async (CreateUrlRequest request, IUrlService service) =>
{
    try
    {
        var response = await service.CreateUrl(request.OriginalUrl);
        return Results.Created($"/api/urls/{response.Code}", response);
    }
    catch (InvalidUrlException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

urls.MapGet("/", async (IUrlService service) => Results.Ok(await service.ListUrls()));

urls.MapGet("/{code}", async (string code, IUrlService service) =>
{
    var response = await service.GetUrl(code);
    return response is null ? Results.NotFound() : Results.Ok(response);
});

urls.MapPut("/{code}", async (string code, UpdateUrlRequest request, IUrlService service) =>
{
    try
    {
        var response = await service.UpdateUrl(code, request.OriginalUrl, request.IsActive);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }
    catch (InvalidUrlException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

urls.MapDelete("/{code}", async (string code, IUrlService service) =>
    await service.DeleteUrl(code) ? Results.NoContent() : Results.NotFound());

// Redirect endpoint lives at the root so short links look like gul.fy/{code}, not gul.fy/api/urls/{code}.
app.MapGet("/{code}", async (string code, IUrlService service) =>
{
    var record = await service.GetRedirectTarget(code);
    if (record is null) return Results.NotFound();
    if (!record.IsActive) return Results.StatusCode(StatusCodes.Status410Gone);

    return Results.Redirect(record.OriginalUrl);
});

app.Run();

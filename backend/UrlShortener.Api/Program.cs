var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "UrlShortener.Api"
}))
.WithName("GetHealth");

app.Run();

namespace UrlShortener.Api.Handlers;

public static class HealthHandler
{
    public static IResult GetHealth() => Results.Ok(new
    {
        status = "healthy",
        service = "UrlShortener.Api"
    });
}

using UrlShortener.Api.DTOs;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Handlers;

public static class UrlHandler
{
    public static async Task<IResult> CreateUrl(CreateUrlRequest request, IUrlService service)
    {
        try
        {
            var response = await service.CreateUrl(request.OriginalUrl, request.PlatformTargets, request.CustomCode);
            return Results.Created($"/api/urls/{response.Code}", response);
        }
        catch (InvalidUrlException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public static async Task<IResult> ListUrls(IUrlService service)
    {
        return Results.Ok(await service.ListUrls());
    }

    public static async Task<IResult> GetUrl(string code, IUrlService service)
    {
        var response = await service.GetUrl(code);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    public static async Task<IResult> UpdateUrl(string code, UpdateUrlRequest request, IUrlService service)
    {
        try
        {
            var response = await service.UpdateUrl(code, request.OriginalUrl, request.IsActive, request.PlatformTargets, request.CustomCode);
            return response is null ? Results.NotFound() : Results.Ok(response);
        }
        catch (InvalidUrlException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public static async Task<IResult> DeleteUrl(string code, IUrlService service)
    {
        return await service.DeleteUrl(code) ? Results.NoContent() : Results.NotFound();
    }

    public static async Task<IResult> RedirectToDestination(HttpRequest request, string code, IUrlService service)
    {
        var target = await service.GetRedirectTarget(code, request.Headers.UserAgent.ToString());
        if (target is null) return Results.NotFound();
        if (!target.IsActive) return Results.StatusCode(StatusCodes.Status410Gone);

        return Results.Redirect(target.DestinationUrl);
    }
}

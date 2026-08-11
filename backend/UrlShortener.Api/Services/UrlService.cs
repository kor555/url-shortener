using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Services;

public class InvalidUrlException(string message) : Exception(message);

public class UrlService(IUrlRepository repository, IBase62Service base62, IConfiguration configuration) : IUrlService
{
    private string BaseUrl => configuration["ShortUrl:BaseUrl"] ?? "http://localhost:5104";

    public async Task<UrlResponse> CreateUrl(string originalUrl)
    {
        var normalized = NormalizeUrl(originalUrl);
        var record = await repository.InsertUrl(normalized);
        return ToResponse(record);
    }

    public async Task<IReadOnlyList<UrlResponse>> ListUrls()
    {
        var records = await repository.GetAllUrls();
        return records.Select(ToResponse).ToList();
    }

    public async Task<UrlResponse?> GetUrl(string code)
    {
        if (!base62.TryDecode(code, out var id)) return null;

        var record = await repository.GetUrlById(id);
        return record is null ? null : ToResponse(record);
    }

    public async Task<UrlResponse?> UpdateUrl(string code, string? originalUrl, bool? isActive)
    {
        if (!base62.TryDecode(code, out var id)) return null;

        var normalized = originalUrl is null ? null : NormalizeUrl(originalUrl);
        var record = await repository.UpdateUrl(id, normalized, isActive);
        return record is null ? null : ToResponse(record);
    }

    public async Task<bool> DeleteUrl(string code)
    {
        if (!base62.TryDecode(code, out var id)) return false;
        return await repository.DeleteUrl(id);
    }

    public async Task<UrlRecord?> GetRedirectTarget(string code)
    {
        if (!base62.TryDecode(code, out var id)) return null;
        return await repository.GetUrlById(id);
    }

    private UrlResponse ToResponse(UrlRecord record)
    {
        var code = base62.Encode(record.Id);
        return new UrlResponse(
            record.Id,
            code,
            $"{BaseUrl.TrimEnd('/')}/{code}",
            record.OriginalUrl,
            record.IsActive,
            record.CreatedAt,
            record.UpdatedAt);
    }

    private static string NormalizeUrl(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.Length > 0 &&
            Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        // No scheme given (e.g. "example.com/path") — default to https instead of rejecting it.
        if (trimmed.Length > 0 &&
            Uri.TryCreate($"https://{trimmed}", UriKind.Absolute, out var httpsUri) &&
            httpsUri.Scheme == Uri.UriSchemeHttps)
        {
            return httpsUri.ToString();
        }

        throw new InvalidUrlException("originalUrl must be a valid http/https URL");
    }
}

namespace UrlShortener.Api.DTOs;

public record CreateUrlRequest(string OriginalUrl);

public record UpdateUrlRequest(string? OriginalUrl, bool? IsActive);

public record UrlResponse(
    long Id,
    string Code,
    string ShortUrl,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Raw row shape passed between the repository and service, before the service
// adds the derived Code/ShortUrl fields that make up the public UrlResponse.
public record UrlRecord(
    long Id,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IUrlService
{
    Task<UrlResponse> CreateUrl(string originalUrl);
    Task<IReadOnlyList<UrlResponse>> ListUrls();
    Task<UrlResponse?> GetUrl(string code);
    Task<UrlResponse?> UpdateUrl(string code, string? originalUrl, bool? isActive);
    Task<bool> DeleteUrl(string code);
    Task<UrlRecord?> GetRedirectTarget(string code);
}

public interface IUrlRepository
{
    Task EnsureUrlsTableExists();
    Task<UrlRecord> InsertUrl(string originalUrl);
    Task<IReadOnlyList<UrlRecord>> GetAllUrls();
    Task<UrlRecord?> GetUrlById(long id);
    Task<UrlRecord?> UpdateUrl(long id, string? originalUrl, bool? isActive);
    Task<bool> DeleteUrl(long id);
}

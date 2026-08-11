namespace UrlShortener.Api.DTOs;

// A per-platform override, e.g. ("android", "https://play.google.com/..."). Platform
// is a free-form key (not an enum) so new platforms can be added without a schema change.
public record PlatformTarget(string Platform, string Url);

public record CreateUrlRequest(string OriginalUrl, IReadOnlyList<PlatformTarget>? PlatformTargets = null);

public record UpdateUrlRequest(string? OriginalUrl, bool? IsActive, IReadOnlyList<PlatformTarget>? PlatformTargets = null);

public record UrlResponse(
    long Id,
    string Code,
    string ShortUrl,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PlatformTarget> PlatformTargets);

// Raw row shape passed between the repository and service, before the service
// adds the derived Code/ShortUrl fields that make up the public UrlResponse.
public record UrlRecord(
    long Id,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PlatformTarget> PlatformTargets);

// Result of resolving a short code to where it should redirect, after picking
// between the default OriginalUrl and any matching platform override.
public record RedirectTarget(bool IsActive, string DestinationUrl);

public interface IUrlService
{
    Task<UrlResponse> CreateUrl(string originalUrl, IReadOnlyList<PlatformTarget>? platformTargets = null);
    Task<IReadOnlyList<UrlResponse>> ListUrls();
    Task<UrlResponse?> GetUrl(string code);
    Task<UrlResponse?> UpdateUrl(string code, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets = null);
    Task<bool> DeleteUrl(string code);
    Task<RedirectTarget?> GetRedirectTarget(string code, string? userAgent);
}

public interface IUrlRepository
{
    Task EnsureUrlsTableExists();
    Task<UrlRecord> InsertUrl(string originalUrl, IReadOnlyList<PlatformTarget> platformTargets);
    Task<IReadOnlyList<UrlRecord>> GetAllUrls();
    Task<UrlRecord?> GetUrlById(long id);

    // platformTargets: null leaves existing platform targets untouched, a (possibly empty) list replaces them.
    Task<UrlRecord?> UpdateUrl(long id, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets);
    Task<bool> DeleteUrl(long id);
}

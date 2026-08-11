namespace UrlShortener.Api.DTOs;

// A per-platform override, e.g. ("android", "https://play.google.com/..."). Platform
// is a free-form key (not an enum) so new platforms can be added without a schema change.
public record PlatformTarget(string Platform, string Url);

// Read-side view of a platform target, with its server-tracked click count.
public record PlatformTargetView(string Platform, string Url, long ClickCount);

// CustomCode: null means no custom name (use the auto-generated base62 code).
public record CreateUrlRequest(string OriginalUrl, IReadOnlyList<PlatformTarget>? PlatformTargets = null, string? CustomCode = null);

// CustomCode: null leaves the current name untouched, "" clears it back to the
// auto-generated code, anything else sets/changes the custom name.
public record UpdateUrlRequest(string? OriginalUrl, bool? IsActive, IReadOnlyList<PlatformTarget>? PlatformTargets = null, string? CustomCode = null);

public record UrlResponse(
    long Id,
    string Code,
    string ShortUrl,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long ViewCount,
    string? CustomCode,
    IReadOnlyList<PlatformTargetView> PlatformTargets);

// Raw row shape passed between the repository and service, before the service
// adds the derived Code/ShortUrl fields that make up the public UrlResponse.
public record UrlRecord(
    long Id,
    string OriginalUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long ViewCount,
    string? CustomCode,
    IReadOnlyList<PlatformTargetView> PlatformTargets);

// Result of resolving a short code to where it should redirect, after picking
// between the default OriginalUrl and any matching platform override.
public record RedirectTarget(bool IsActive, string DestinationUrl);

public interface IUrlService
{
    Task<UrlResponse> CreateUrl(string originalUrl, IReadOnlyList<PlatformTarget>? platformTargets = null, string? customCode = null);
    Task<IReadOnlyList<UrlResponse>> ListUrls();
    Task<UrlResponse?> GetUrl(string code);
    Task<UrlResponse?> UpdateUrl(string code, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets = null, string? customCode = null);
    Task<bool> DeleteUrl(string code);
    Task<RedirectTarget?> GetRedirectTarget(string code, string? userAgent);
}

public interface IUrlRepository
{
    Task EnsureUrlsTableExists();
    Task<UrlRecord> InsertUrl(string originalUrl, IReadOnlyList<PlatformTarget> platformTargets, string? customCode);
    Task<IReadOnlyList<UrlRecord>> GetAllUrls();
    Task<UrlRecord?> GetUrlById(long id);
    Task<UrlRecord?> GetUrlByCustomCode(string customCode);

    // platformTargets: null leaves existing platform targets untouched, a (possibly empty) list replaces them.
    // customCode: null leaves it untouched, "" clears it to NULL, anything else sets it.
    Task<UrlRecord?> UpdateUrl(long id, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets, string? customCode);
    Task<bool> DeleteUrl(long id);

    // Records a visit: always increments the link's total view count, and also bumps the
    // matching platform target's click count when matchedPlatform names one that exists.
    Task RecordVisit(long id, string? matchedPlatform);
}

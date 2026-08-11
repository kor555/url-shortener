using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Services;

public class InvalidUrlException(string message) : Exception(message);

public class UrlService(IUrlRepository repository, IBase62Service base62, IConfiguration configuration) : IUrlService
{
    private string BaseUrl => configuration["ShortUrl:BaseUrl"] ?? "http://localhost:5104";

    public async Task<UrlResponse> CreateUrl(string originalUrl, IReadOnlyList<PlatformTarget>? platformTargets = null)
    {
        var normalized = NormalizeUrl(originalUrl);
        var normalizedTargets = NormalizePlatformTargets(platformTargets);
        var record = await repository.InsertUrl(normalized, normalizedTargets);
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

    public async Task<UrlResponse?> UpdateUrl(string code, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets = null)
    {
        if (!base62.TryDecode(code, out var id)) return null;

        var normalized = originalUrl is null ? null : NormalizeUrl(originalUrl);
        var normalizedTargets = platformTargets is null ? null : NormalizePlatformTargets(platformTargets);
        var record = await repository.UpdateUrl(id, normalized, isActive, normalizedTargets);
        return record is null ? null : ToResponse(record);
    }

    public async Task<bool> DeleteUrl(string code)
    {
        if (!base62.TryDecode(code, out var id)) return false;
        return await repository.DeleteUrl(id);
    }

    public async Task<RedirectTarget?> GetRedirectTarget(string code, string? userAgent)
    {
        if (!base62.TryDecode(code, out var id)) return null;

        var record = await repository.GetUrlById(id);
        if (record is null) return null;

        var platform = DetectPlatform(userAgent);
        var destination = record.PlatformTargets.FirstOrDefault(t => t.Platform == platform)?.Url ?? record.OriginalUrl;
        return new RedirectTarget(record.IsActive, destination);
    }

    // Only android/ios are recognized today; unmatched user agents fall back to the default URL.
    private static string? DetectPlatform(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return null;
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "android";
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase)) return "ios";
        return null;
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
            record.UpdatedAt,
            record.PlatformTargets);
    }

    private static IReadOnlyList<PlatformTarget> NormalizePlatformTargets(IReadOnlyList<PlatformTarget>? targets)
    {
        if (targets is null || targets.Count == 0) return [];

        var seenPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<PlatformTarget>();
        foreach (var target in targets)
        {
            var platform = target.Platform.Trim().ToLowerInvariant();
            if (platform.Length == 0) throw new InvalidUrlException("platform name cannot be empty");
            if (!seenPlatforms.Add(platform)) throw new InvalidUrlException($"duplicate platform target: {platform}");

            normalized.Add(new PlatformTarget(platform, NormalizeUrl(target.Url)));
        }
        return normalized;
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

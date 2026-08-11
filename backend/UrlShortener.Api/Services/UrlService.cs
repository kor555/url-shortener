using System.Text.RegularExpressions;
using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Services;

public class InvalidUrlException(string message) : Exception(message);

public partial class UrlService(IUrlRepository repository, IBase62Service base62, IConfiguration configuration) : IUrlService
{
    private string BaseUrl => configuration["ShortUrl:BaseUrl"] ?? "http://localhost:5104";

    public async Task<UrlResponse> CreateUrl(string originalUrl, IReadOnlyList<PlatformTarget>? platformTargets = null, string? customCode = null)
    {
        var normalized = NormalizeUrl(originalUrl);
        var normalizedTargets = NormalizePlatformTargets(platformTargets);

        string? normalizedCode = null;
        if (!string.IsNullOrWhiteSpace(customCode))
        {
            normalizedCode = await ValidateCustomCode(customCode, currentId: null);
        }

        var record = await repository.InsertUrl(normalized, normalizedTargets, normalizedCode);
        return ToResponse(record);
    }

    public async Task<IReadOnlyList<UrlResponse>> ListUrls()
    {
        var records = await repository.GetAllUrls();
        return records.Select(ToResponse).ToList();
    }

    public async Task<UrlResponse?> GetUrl(string code)
    {
        var record = await ResolveByCode(code);
        return record is null ? null : ToResponse(record);
    }

    public async Task<UrlResponse?> UpdateUrl(string code, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets = null, string? customCode = null)
    {
        var existing = await ResolveByCode(code);
        if (existing is null) return null;

        var normalized = originalUrl is null ? null : NormalizeUrl(originalUrl);
        var normalizedTargets = platformTargets is null ? null : NormalizePlatformTargets(platformTargets);

        string? codeToPersist = null;
        if (customCode is not null)
        {
            var trimmed = customCode.Trim();
            codeToPersist = trimmed.Length == 0 ? "" : await ValidateCustomCode(trimmed, existing.Id);
        }

        var record = await repository.UpdateUrl(existing.Id, normalized, isActive, normalizedTargets, codeToPersist);
        return record is null ? null : ToResponse(record);
    }

    public async Task<bool> DeleteUrl(string code)
    {
        var existing = await ResolveByCode(code);
        return existing is not null && await repository.DeleteUrl(existing.Id);
    }

    public async Task<RedirectTarget?> GetRedirectTarget(string code, string? userAgent)
    {
        var record = await ResolveByCode(code);
        if (record is null) return null;

        var platform = DetectPlatform(userAgent);
        var matchedTarget = record.PlatformTargets.FirstOrDefault(t => t.Platform == platform);
        var destination = matchedTarget?.Url ?? record.OriginalUrl;

        // Deactivated links 410 instead of redirecting, so they shouldn't rack up clicks either.
        if (record.IsActive)
        {
            await repository.RecordVisit(record.Id, matchedTarget?.Platform);
        }

        return new RedirectTarget(record.IsActive, destination);
    }

    // A code is either a custom name someone chose, or the base62 encoding of the row id.
    private async Task<UrlRecord?> ResolveByCode(string code)
    {
        var byCustomCode = await repository.GetUrlByCustomCode(code);
        if (byCustomCode is not null) return byCustomCode;

        return base62.TryDecode(code, out var id) ? await repository.GetUrlById(id) : null;
    }

    // Rejects a custom name that's already taken, either as another link's custom name
    // or as another link's auto-generated base62 code (which would make that code ambiguous).
    private async Task<string> ValidateCustomCode(string customCode, long? currentId)
    {
        if (!CustomCodePattern().IsMatch(customCode))
        {
            throw new InvalidUrlException("custom name may only contain letters, numbers, hyphens, and underscores");
        }

        var byCustomCode = await repository.GetUrlByCustomCode(customCode);
        if (byCustomCode is not null && byCustomCode.Id != currentId)
        {
            throw new InvalidUrlException($"This custom name \"{customCode}\" is already taken.");
        }

        if (base62.TryDecode(customCode, out var decodedId))
        {
            var byId = await repository.GetUrlById(decodedId);
            if (byId is not null && byId.Id != currentId)
            {
                throw new InvalidUrlException($"This custom name \"{customCode}\" is already taken.");
            }
        }

        return customCode;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex CustomCodePattern();

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
        var code = record.CustomCode ?? base62.Encode(record.Id);
        return new UrlResponse(
            record.Id,
            code,
            $"{BaseUrl.TrimEnd('/')}/{code}",
            record.OriginalUrl,
            record.IsActive,
            record.CreatedAt,
            record.UpdatedAt,
            record.ViewCount,
            record.CustomCode,
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

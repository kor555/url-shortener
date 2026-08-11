using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Tests;

// Hand-rolled in-memory stand-in for IUrlRepository, mirroring the real UrlRepository's
// tri-state update semantics, so UrlService can be unit tested without a real database.
public class FakeUrlRepository : IUrlRepository
{
    private readonly List<UrlRecord> _rows = [];
    private long _nextId = 1;

    public List<(long Id, string? MatchedPlatform)> RecordedVisits { get; } = [];

    public Task EnsureUrlsTableExists() => Task.CompletedTask;

    public Task<UrlRecord> InsertUrl(string originalUrl, IReadOnlyList<PlatformTarget> platformTargets, string? customCode)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new UrlRecord(
            _nextId++,
            originalUrl,
            true,
            now,
            now,
            0,
            customCode,
            platformTargets.Select(t => new PlatformTargetView(t.Platform, t.Url, 0)).ToList());
        _rows.Add(record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<UrlRecord>> GetAllUrls() =>
        Task.FromResult<IReadOnlyList<UrlRecord>>(_rows.OrderByDescending(r => r.Id).ToList());

    public Task<UrlRecord?> GetUrlById(long id) =>
        Task.FromResult(_rows.FirstOrDefault(r => r.Id == id));

    public Task<UrlRecord?> GetUrlByCustomCode(string customCode) =>
        Task.FromResult(_rows.FirstOrDefault(r => r.CustomCode == customCode));

    public Task<UrlRecord?> UpdateUrl(long id, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets, string? customCode)
    {
        var index = _rows.FindIndex(r => r.Id == id);
        if (index < 0) return Task.FromResult<UrlRecord?>(null);

        var existing = _rows[index];
        var newTargets = platformTargets is null
            ? existing.PlatformTargets
            : platformTargets.Select(t => new PlatformTargetView(t.Platform, t.Url, 0)).ToList();
        var newCustomCode = customCode switch
        {
            null => existing.CustomCode,
            "" => null,
            _ => customCode,
        };

        var updated = existing with
        {
            OriginalUrl = originalUrl ?? existing.OriginalUrl,
            IsActive = isActive ?? existing.IsActive,
            CustomCode = newCustomCode,
            PlatformTargets = newTargets,
        };
        _rows[index] = updated;
        return Task.FromResult<UrlRecord?>(updated);
    }

    public Task<bool> DeleteUrl(long id) => Task.FromResult(_rows.RemoveAll(r => r.Id == id) > 0);

    public Task RecordVisit(long id, string? matchedPlatform)
    {
        RecordedVisits.Add((id, matchedPlatform));

        var index = _rows.FindIndex(r => r.Id == id);
        if (index < 0) return Task.CompletedTask;

        var existing = _rows[index];
        var targets = matchedPlatform is null
            ? existing.PlatformTargets
            : existing.PlatformTargets
                .Select(t => t.Platform == matchedPlatform ? t with { ClickCount = t.ClickCount + 1 } : t)
                .ToList();

        _rows[index] = existing with { ViewCount = existing.ViewCount + 1, PlatformTargets = targets };
        return Task.CompletedTask;
    }
}

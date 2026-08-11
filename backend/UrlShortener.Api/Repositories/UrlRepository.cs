using Npgsql;
using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Repositories;

public class UrlRepository(NpgsqlDataSource db) : IUrlRepository
{
    public async Task EnsureUrlsTableExists()
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS urls (
                id BIGSERIAL PRIMARY KEY,
                original_url TEXT NOT NULL,
                is_active BOOLEAN NOT NULL DEFAULT true,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS url_platform_targets (
                id BIGSERIAL PRIMARY KEY,
                url_id BIGINT NOT NULL REFERENCES urls(id) ON DELETE CASCADE,
                platform TEXT NOT NULL,
                target_url TEXT NOT NULL,
                UNIQUE (url_id, platform)
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<UrlRecord> InsertUrl(string originalUrl, IReadOnlyList<PlatformTarget> platformTargets)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        var row = await InsertUrlRow(conn, transaction, originalUrl);
        await ReplacePlatformTargets(conn, transaction, row.Id, platformTargets);

        await transaction.CommitAsync();
        return row with { PlatformTargets = platformTargets };
    }

    public async Task<IReadOnlyList<UrlRecord>> GetAllUrls()
    {
        await using var conn = await db.OpenConnectionAsync();

        var rows = new List<UrlRecord>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, original_url, is_active, created_at, updated_at FROM urls ORDER BY id DESC";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(MapRow(reader, []));
            }
        }

        var result = new List<UrlRecord>();
        foreach (var row in rows)
        {
            var targets = await GetPlatformTargets(conn, row.Id);
            result.Add(row with { PlatformTargets = targets });
        }
        return result;
    }

    public async Task<UrlRecord?> GetUrlById(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        var row = await SelectUrlRow(conn, id);
        if (row is null) return null;

        var targets = await GetPlatformTargets(conn, id);
        return row with { PlatformTargets = targets };
    }

    public async Task<UrlRecord?> UpdateUrl(long id, string? originalUrl, bool? isActive, IReadOnlyList<PlatformTarget>? platformTargets)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            UPDATE urls
            SET original_url = COALESCE($1, original_url),
                is_active = COALESCE($2, is_active),
                updated_at = now()
            WHERE id = $3
            RETURNING id, original_url, is_active, created_at, updated_at
            """;
        cmd.Parameters.AddWithValue((object?)originalUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)isActive ?? DBNull.Value);
        cmd.Parameters.AddWithValue(id);

        UrlRecord? row = null;
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                row = MapRow(reader, []);
            }
        }

        if (row is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        if (platformTargets is not null)
        {
            await ReplacePlatformTargets(conn, transaction, id, platformTargets);
        }

        await transaction.CommitAsync();

        var finalTargets = platformTargets ?? await GetPlatformTargets(conn, id);
        return row with { PlatformTargets = finalTargets };
    }

    public async Task<bool> DeleteUrl(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM urls WHERE id = $1";
        cmd.Parameters.AddWithValue(id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private static async Task<UrlRecord> InsertUrlRow(NpgsqlConnection conn, NpgsqlTransaction transaction, string originalUrl)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO urls (original_url) VALUES ($1)
            RETURNING id, original_url, is_active, created_at, updated_at
            """;
        cmd.Parameters.AddWithValue(originalUrl);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return MapRow(reader, []);
    }

    private static async Task<UrlRecord?> SelectUrlRow(NpgsqlConnection conn, long id)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, original_url, is_active, created_at, updated_at FROM urls WHERE id = $1";
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRow(reader, []) : null;
    }

    private static async Task ReplacePlatformTargets(NpgsqlConnection conn, NpgsqlTransaction transaction, long urlId, IReadOnlyList<PlatformTarget> targets)
    {
        await using var deleteCmd = conn.CreateCommand();
        deleteCmd.Transaction = transaction;
        deleteCmd.CommandText = "DELETE FROM url_platform_targets WHERE url_id = $1";
        deleteCmd.Parameters.AddWithValue(urlId);
        await deleteCmd.ExecuteNonQueryAsync();

        foreach (var target in targets)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = "INSERT INTO url_platform_targets (url_id, platform, target_url) VALUES ($1, $2, $3)";
            insertCmd.Parameters.AddWithValue(urlId);
            insertCmd.Parameters.AddWithValue(target.Platform);
            insertCmd.Parameters.AddWithValue(target.Url);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<IReadOnlyList<PlatformTarget>> GetPlatformTargets(NpgsqlConnection conn, long urlId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT platform, target_url FROM url_platform_targets WHERE url_id = $1 ORDER BY platform";
        cmd.Parameters.AddWithValue(urlId);
        await using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<PlatformTarget>();
        while (await reader.ReadAsync())
        {
            list.Add(new PlatformTarget(reader.GetString(0), reader.GetString(1)));
        }
        return list;
    }

    private static UrlRecord MapRow(NpgsqlDataReader reader, IReadOnlyList<PlatformTarget> platformTargets) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetBoolean(2),
        reader.GetFieldValue<DateTimeOffset>(3),
        reader.GetFieldValue<DateTimeOffset>(4),
        platformTargets);
}

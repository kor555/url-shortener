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
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<UrlRecord> InsertUrl(string originalUrl)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO urls (original_url) VALUES ($1)
            RETURNING id, original_url, is_active, created_at, updated_at
            """;
        cmd.Parameters.AddWithValue(originalUrl);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return MapRow(reader);
    }

    public async Task<IReadOnlyList<UrlRecord>> GetAllUrls()
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, original_url, is_active, created_at, updated_at FROM urls ORDER BY id DESC";
        await using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<UrlRecord>();
        while (await reader.ReadAsync())
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task<UrlRecord?> GetUrlById(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, original_url, is_active, created_at, updated_at FROM urls WHERE id = $1";
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRow(reader) : null;
    }

    public async Task<UrlRecord?> UpdateUrl(long id, string? originalUrl, bool? isActive)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
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
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRow(reader) : null;
    }

    public async Task<bool> DeleteUrl(long id)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM urls WHERE id = $1";
        cmd.Parameters.AddWithValue(id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private static UrlRecord MapRow(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetBoolean(2),
        reader.GetFieldValue<DateTimeOffset>(3),
        reader.GetFieldValue<DateTimeOffset>(4));
}

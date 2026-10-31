using System.IO;
using Microsoft.Data.Sqlite;

namespace MiniInfos.Providers.Antigravity;

/// <summary>Reads only usage metadata, never step_payload, prompts, search indexes or authentication files.</summary>
internal static class AntigravityDatabase
{
    public static IReadOnlyList<LoggedResponse> Read(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 1,
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA trusted_schema=OFF;";
        command.ExecuteNonQuery();
        string? project = null;
        command.CommandText = "SELECT data FROM trajectory_metadata_blob WHERE id='main' AND length(data)<=65536 LIMIT 1";
        try { if (command.ExecuteScalar() is byte[] metadata) project = AntigravityResponses.Project(metadata); }
        catch (SqliteException e) when (e.SqliteErrorCode == 1) { /* Optional table in older schemas. */ }

        command.CommandText = "SELECT idx, metadata FROM steps WHERE length(metadata)<=65536 ORDER BY idx";
        using var reader = command.ExecuteReader();
        var session = Path.GetFileNameWithoutExtension(path);
        var responses = new List<LoggedResponse>();
        while (reader.Read())
            if (reader.GetValue(1) is byte[] bytes && AntigravityResponses.Read(session, reader.GetInt64(0), bytes, project) is { } response)
                responses.Add(response);
        return responses;
    }
}

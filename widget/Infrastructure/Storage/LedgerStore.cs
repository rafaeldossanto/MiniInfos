using System.IO;
using System.Text.Json;

namespace MiniInfos.Infrastructure.Storage;

/// <summary>
/// A ledger's file: read whole at start and written whole and atomically, at most every 30 s
/// unless flushed, so a scan every few seconds doesn't rewrite it on each pass.
/// </summary>
public sealed class LedgerStore<T>(string path) where T : class
{
    private static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(30);

    private DateTimeOffset _savedAt = DateTimeOffset.MinValue;
    private bool _isDirty;

    public T? Load()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Changed() => _isDirty = true;

    public void SaveIfDue(T state)
    {
        if (_isDirty && DateTimeOffset.Now - _savedAt >= SaveEvery) Save(state);
    }

    public void Flush(T state)
    {
        if (_isDirty) Save(state);
    }

    private void Save(T state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, path, overwrite: true);
            _savedAt = DateTimeOffset.Now;
            _isDirty = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Kept dirty: the next scan saves it.
        }
    }
}

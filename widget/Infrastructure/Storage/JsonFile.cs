using System.IO;
using System.Text.Json;

namespace MiniInfos.Infrastructure.Storage;

/// <summary>
/// One settings file, written whole: missing or unreadable, it reads as null, and a failed write is
/// let go, since settings are never worth stopping the card for.
/// </summary>
public sealed class JsonFile<T>(string path) where T : class
{
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

    public void Save(T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

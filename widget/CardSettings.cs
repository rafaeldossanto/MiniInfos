using System.IO;
using System.Text.Json;

namespace MiniInfos;

/// <summary>Where the card was left, how big, and which view it shows.</summary>
/// <param name="Scale">The card's size over its design size.</param>
/// <param name="IsDetailed">Whole numbers rolling up, instead of the short 1,4B form.</param>
public sealed record CardSettings(double Left, double Top, double Scale = 1, bool IsDetailed = false)
{
    private static readonly string StorePath = Path.Combine(ClaudePaths.AppData, "window.json");

    public static CardSettings? Load()
    {
        try
        {
            return JsonSerializer.Deserialize<CardSettings>(File.ReadAllText(StorePath));
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ClaudePaths.AppData);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(this));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

using System.IO;

namespace MiniInfos;

/// <summary>Where a card was left, how big, and what it shows.</summary>
/// <param name="Scale">The card's size over its design size.</param>
/// <param name="IsDetailed">Whole numbers rolling up, instead of the short 1,4B form.</param>
/// <param name="Page">The id of the provider whose page was on screen (<see cref="IUsageProvider.Id"/>).</param>
/// <param name="BesidePage">In "Visualização dividida", the id of the page shown beside it.</param>
public sealed record CardSettings(
    double Left,
    double Top,
    double Scale = 1,
    bool IsDetailed = false,
    string? Page = null,
    string? BesidePage = null);

/// <summary>
/// Where one card keeps its settings: window.json for the main card, which every version reads,
/// and one file under windows\ for each card opened with "Nova janela", so it comes back too.
/// </summary>
public sealed class CardStore
{
    private readonly JsonFile<CardSettings> _file;

    private CardStore(string path) => _file = new JsonFile<CardSettings>(path);

    public static CardStore Main { get; } = new(Path.Combine(AppPaths.Data, "window.json"));

    private static string ExtrasFolder => Path.Combine(AppPaths.Data, "windows");

    /// <summary>The cards opened with "Nova janela" that were still open when the app last closed.</summary>
    public static IReadOnlyList<CardStore> Extras() =>
        Directory.Exists(ExtrasFolder)
            ? [.. Directory.EnumerateFiles(ExtrasFolder, "*.json").Order().Select(path => new CardStore(path))]
            : [];

    /// <summary>A new card's place, its settings written right away so it comes back if the app closes.</summary>
    public static CardStore NewExtra(CardSettings settings)
    {
        var store = new CardStore(Path.Combine(ExtrasFolder, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json"));
        store.Save(settings);
        return store;
    }

    public CardSettings? Load() => _file.Load();

    public void Save(CardSettings settings) => _file.Save(settings);

    /// <summary>Drops a card that was closed for good: it does not come back.</summary>
    public void Forget() => _file.Delete();
}

using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace MiniInfos;

/// <summary>Embedded families, stored by name so future additions do not change saved choices.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FontChoice>))]
public enum FontChoice
{
    Manrope,
    GeistMono,
    Cinzel,
    BodoniModa,
    ChakraPetch,
    Orbitron,
}

public sealed record FontOption(FontChoice Choice, string Name, FontFamily Family);

/// <summary>Typography is independent of the color theme and shared by every card.</summary>
public static class AppFonts
{
    private static readonly Uri FontRoot = new("pack://application:,,,/MiniInfos;component/");

    private static readonly IReadOnlyDictionary<FontChoice, FontOption> Families = new Dictionary<FontChoice, FontOption>
    {
        [FontChoice.Manrope] = Option(FontChoice.Manrope, "Manrope"),
        [FontChoice.GeistMono] = Option(FontChoice.GeistMono, "Geist Mono"),
        [FontChoice.Cinzel] = Option(FontChoice.Cinzel, "Cinzel"),
        [FontChoice.BodoniModa] = Option(FontChoice.BodoniModa, "Bodoni Moda"),
        [FontChoice.ChakraPetch] = Option(FontChoice.ChakraPetch, "Chakra Petch"),
        [FontChoice.Orbitron] = Option(FontChoice.Orbitron, "Orbitron"),
    };

    public static IReadOnlyList<FontOption> TextOptions { get; } =
        [Families[FontChoice.Manrope], Families[FontChoice.Cinzel], Families[FontChoice.ChakraPetch]];

    public static IReadOnlyList<FontOption> NumberOptions { get; } =
        [Families[FontChoice.Manrope], Families[FontChoice.GeistMono], Families[FontChoice.BodoniModa], Families[FontChoice.Orbitron]];

    public static event Action? Changed;

    public static FontFamily Family(FontChoice choice) =>
        Families.TryGetValue(choice, out var option) ? option.Family : Families[FontChoice.Manrope].Family;

    public static void Follow(Preferences preferences)
    {
        Apply(preferences);
        preferences.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName is nameof(Preferences.TextFont) or nameof(Preferences.NumberFont)) Apply(preferences);
        };
    }

    private static FontOption Option(FontChoice choice, string name) =>
        new(choice, name, new FontFamily(FontRoot, "./Assets/Fonts/#" + name));

    private static void Apply(Preferences preferences)
    {
        var resources = Application.Current.Resources;
        resources["CardFont"] = Family(preferences.TextFont);
        resources["NumberFont"] = Family(preferences.NumberFont);
        Changed?.Invoke();
    }
}

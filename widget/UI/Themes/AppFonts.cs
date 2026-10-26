using System.Windows.Media;
using System.Windows;
using MiniInfos.Settings;

namespace MiniInfos.UI.Themes;

/// <param name="HasSmallCapsLowercase">Its lowercase letters are drawn as small capitals (Cinzel), so a capital inside a word stands out.</param>
public sealed record FontOption(FontChoice Choice, string Name, FontFamily Family, bool HasSmallCapsLowercase = false);

/// <summary>Typography is independent of the color theme and shared by every card.</summary>
public static class AppFonts
{
    private static readonly Uri FontRoot = new("pack://application:,,,/MiniInfos;component/");

    private static readonly IReadOnlyDictionary<FontChoice, FontOption> Families = new Dictionary<FontChoice, FontOption>
    {
        [FontChoice.Manrope] = Option(FontChoice.Manrope, "Manrope"),
        [FontChoice.GeistMono] = Option(FontChoice.GeistMono, "Geist Mono"),
        [FontChoice.Cinzel] = Option(FontChoice.Cinzel, "Cinzel", hasSmallCapsLowercase: true),
        [FontChoice.BodoniModa] = Option(FontChoice.BodoniModa, "Bodoni Moda"),
        [FontChoice.ChakraPetch] = Option(FontChoice.ChakraPetch, "Chakra Petch"),
        [FontChoice.Orbitron] = Option(FontChoice.Orbitron, "Orbitron"),
    };

    public static IReadOnlyList<FontOption> TextOptions { get; } =
        [Families[FontChoice.Manrope], Families[FontChoice.Cinzel], Families[FontChoice.ChakraPetch]];

    public static IReadOnlyList<FontOption> NumberOptions { get; } =
        [Families[FontChoice.Manrope], Families[FontChoice.GeistMono], Families[FontChoice.BodoniModa], Families[FontChoice.Orbitron]];

    private static FontOption _text = Families[FontChoice.Manrope];

    public static event Action? Changed;

    public static FontFamily Family(FontChoice choice) => Find(choice).Family;

    /// <summary>
    /// The text as the chosen text font should get it. A font that draws lowercase as small capitals
    /// keeps only the capitals that open a word, so "MiniInfos" and "GPT" don't stand out mid-word.
    /// </summary>
    public static string Cased(string text) => _text.HasSmallCapsLowercase ? InitialCapitalsOnly(text) : text;

    /// <summary>Keeps a capital only where it opens a word: "Codex (GPT)" reads "Codex (Gpt)".</summary>
    public static string InitialCapitalsOnly(string text)
    {
        var letters = text.ToCharArray();
        for (var index = 1; index < letters.Length; index++)
        {
            if (char.IsLetter(letters[index - 1])) letters[index] = char.ToLowerInvariant(letters[index]);
        }

        return new string(letters);
    }

    public static void Follow(Preferences preferences)
    {
        Apply(preferences);
        preferences.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName is nameof(Preferences.TextFont) or nameof(Preferences.NumberFont)) Apply(preferences);
        };
    }

    private static FontOption Find(FontChoice choice) =>
        Families.TryGetValue(choice, out var option) ? option : Families[FontChoice.Manrope];

    private static FontOption Option(FontChoice choice, string name, bool hasSmallCapsLowercase = false) =>
        new(choice, name, new FontFamily(FontRoot, "./Assets/Fonts/#" + name), hasSmallCapsLowercase);

    private static void Apply(Preferences preferences)
    {
        _text = Find(preferences.TextFont);
        var resources = Application.Current.Resources;
        resources["CardFont"] = _text.Family;
        resources["NumberFont"] = Family(preferences.NumberFont);
        Changed?.Invoke();
    }
}

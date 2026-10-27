global using L = MiniInfos.Localization.Localizer;

using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MiniInfos.Settings;

namespace MiniInfos.Localization;

/// <summary>Portuguese source messages with an English catalog, shared by every domain.</summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;
    private static readonly IReadOnlyDictionary<string, string> English = ReadEnglish();
    public static Localizer Current { get; } = new();
    public static CultureInfo Culture => Current.ActiveCulture;
    public CultureInfo ActiveCulture { get; private set; } = Resolve(LanguageChoice.Automatic, SystemCulture);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public static CultureInfo Resolve(LanguageChoice choice, CultureInfo system) =>
        CultureInfo.GetCultureInfo(choice == LanguageChoice.Portuguese
            || choice == LanguageChoice.Automatic && system.TwoLetterISOLanguageName == "pt" ? "pt-BR" : "en-US");

    public static string Text(string source) => Translate(source, Culture);
    public static string Translate(string source, CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "pt" ? source : English.GetValueOrDefault(source, source);

    public static string Format(string source, params object?[] arguments) => string.Format(Culture, Text(source), arguments);

    public static void Follow(Preferences preferences)
    {
        Select(preferences.Language);
        preferences.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(Preferences.Language)) Select(preferences.Language);
        };
    }

    public static void Select(LanguageChoice choice)
    {
        var next = Resolve(choice, SystemCulture);
        if (Current.ActiveCulture.Equals(next)) return;
        Current.ActiveCulture = next;
        Current.PropertyChanged?.Invoke(Current, new(nameof(ActiveCulture)));
        Current.Changed?.Invoke(Current, EventArgs.Empty);
    }

    private static IReadOnlyDictionary<string, string> ReadEnglish()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MiniInfos.Localization.en.json")
            ?? throw new InvalidOperationException("The English translation catalog is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException("The English translation catalog is empty.");
    }
}

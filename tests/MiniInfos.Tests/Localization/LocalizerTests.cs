using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiniInfos.Localization;
using MiniInfos.Settings;
using MiniInfos.Tests.Support;
using Display = MiniInfos.UI.Formatting.Format;

namespace MiniInfos.Tests.Localization;

[CollectionDefinition("Language", DisableParallelization = true)]
public sealed class LanguageCollection;

[Collection("Language")]
public sealed class LocalizerTests
{
    [Theory]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("en-GB", "en-US")]
    [InlineData("es-ES", "en-US")]
    [InlineData("ja-JP", "en-US")]
    [InlineData("", "en-US")]
    public void AutomaticUsesTheDisplayLanguageAndFallsBackToEnglish(string system, string expected) =>
        Assert.Equal(expected, Localizer.Resolve(LanguageChoice.Automatic, CultureInfo.GetCultureInfo(system)).Name);

    [Theory]
    [InlineData(LanguageChoice.English, "pt-BR", "en-US")]
    [InlineData(LanguageChoice.Portuguese, "en-US", "pt-BR")]
    public void ExplicitChoicesOverrideTheComputerLanguage(LanguageChoice choice, string system, string expected) =>
        Assert.Equal(expected, Localizer.Resolve(choice, CultureInfo.GetCultureInfo(system)).Name);

    [Fact]
    public void OlderSettingsKeepTheirThemeAndUseAutomaticLanguage()
    {
        using var folder = new TempFolder();
        var path = folder.File("settings.json");
        File.WriteAllText(path, """{"Theme":"Classic","IsTopmost":false}""");
        var preferences = new Preferences(path);
        Assert.Equal((ThemeChoice.Classic, false, LanguageChoice.Automatic), (preferences.Theme, preferences.IsTopmost, preferences.Language));
        preferences.Language = LanguageChoice.English;
        var saved = new Preferences(path);
        Assert.Equal((ThemeChoice.Classic, false, LanguageChoice.English), (saved.Theme, saved.IsTopmost, saved.Language));
    }

    [Fact]
    public void InvalidNumericLanguageFallsBackToAutomatic()
    {
        using var folder = new TempFolder();
        var path = folder.File("settings.json");
        File.WriteAllText(path, """{"Theme":"Classic","Language":999}""");
        Assert.Equal(LanguageChoice.Automatic, new Preferences(path).Language);
    }

    [Fact]
    public void EnglishFormatsCountsDurationsAndMessagesTogether()
    {
        try
        {
            Localizer.Select(LanguageChoice.English);
            Assert.Equal("1.1M", Display.Count(1_052_000));
            Assert.Equal("1,412,345,678", Display.Full(1_412_345_678));
            Assert.Equal("5h window", Display.WindowLabel(TimeSpan.FromHours(5)));
            Assert.Equal("Weekly", Display.WindowLabel(TimeSpan.FromDays(7)));
            var now = DateTimeOffset.UtcNow;
            Assert.Equal("resets in 2h 13min", Display.Reset(now.AddMinutes(133), now));
            Assert.Equal("3 min ago", Display.Ago(now.AddMinutes(-3), now));
            Assert.Equal("Theme library", Localizer.Text("Biblioteca de temas"));
        }
        finally { Localizer.Select(LanguageChoice.Portuguese); }
    }

    [Fact]
    public void TranslationTemplatesKeepEveryPlaceholder()
    {
        foreach (var (source, english) in EnglishCatalog())
        {
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.Equal(CompositeFormat.Parse(source).MinimumArgumentCount, CompositeFormat.Parse(english).MinimumArgumentCount);
            var placeholders = (string text) => Regex.Matches(text, @"(?<!\{)\{(\d+)[^{}]*\}(?!\})")
                .Select(match => match.Groups[1].Value).Order().ToArray();
            Assert.Equal(placeholders(source), placeholders(english));
        }
    }

    /// <summary>A missing entry falls back to Portuguese silently, so every message written in the app's code is checked here.</summary>
    [Fact]
    public void EveryMessageInTheAppHasAnEnglishTranslation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "widget", "MiniInfos.csproj"))) root = root.Parent;
        Assert.NotNull(root);
        var catalog = EnglishCatalog();
        var messages = new Regex("""(?:L\.(?:Text|Format)|AddTranslated)\(\s*"((?:[^"\\]|\\.)*)"|\{loc:Text\s+'([^']*)'\}""");
        var generated = new[] { $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}" };
        var missing = Directory.EnumerateFiles(Path.Combine(root.FullName, "widget"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs") || path.EndsWith(".xaml"))
            .Where(path => !generated.Any(path.Contains))
            .SelectMany(path => messages.Matches(File.ReadAllText(path)).Select(match => (
                File: Path.GetFileName(path),
                Message: match.Groups[1].Success ? Regex.Unescape(match.Groups[1].Value) : match.Groups[2].Value)))
            .Where(use => !catalog.ContainsKey(use.Message))
            .Select(use => $"{use.File}: {use.Message}")
            .Distinct()
            .ToArray();
        Assert.True(missing.Length == 0, "Messages without an English translation in en.json:\n" + string.Join("\n", missing));
    }

    private static Dictionary<string, string> EnglishCatalog()
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream("MiniInfos.Localization.en.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}

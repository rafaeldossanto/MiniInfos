using System.Globalization;
using MiniInfos.Localization;
using MiniInfos.UI.Themes;

namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckLocalization(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        var theme = preferences.ThemeKey;
        var pages = Find<TabControl>(settings, "Pages");
        pages.SelectedIndex = 0;
        ChooseLanguage(LanguageChoice.English);
        Pump(TimeSpan.FromMilliseconds(500));
        Check(Localizer.Culture.Name == "en-US" && Preferences.Load().Language == LanguageChoice.English,
            "the manual English choice overrides the system and is saved");
        Check(settings.Title == "MiniInfos settings" && (string)Find<TabItem>(settings, "GeneralPage").Header == "General"
            && (string)Find<Button>(settings, "ThemeLibraryButton").Content == "Library",
            "already open settings, tabs and buttons translate without a restart");
        var card = cards.Main;
        var column = Columns(card)[0];
        PumpUntil(() => Find<TextBlock>(column, "ShortLabel").Text == "5h window", TimeSpan.FromSeconds(5));
        Check(Find<TextBlock>(column, "ShortLabel").Text == "5h window" && Find<TextBlock>(column, "LongLabel").Text == "Weekly",
            "the open widget refreshes its usage labels in English");
        pages.SelectedIndex = 1;
        Pump(TimeSpan.FromMilliseconds(100));
        var preview = Find<TextBlock>(settings, "FontPreview");
        var previewText = Shown(preview);
        Check(previewText.Contains("12,345,678"), $"number separators follow English ({previewText})");
        Check(preferences.ThemeKey == theme, "changing language preserves the selected theme");
        var menu = Find<Border>(card, "Card").ContextMenu!;
        menu.PlacementTarget = Find<Border>(card, "Card");
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(100));
        Check(menu.Items.OfType<MenuItem>().Any(item => item.Header as string == "Refresh now")
            && menu.Items.OfType<MenuItem>().Any(item => item.Header as string == "Settings"), "the right-click menu translates too");
        RenderElement(menu, Path.Combine(pictures, "context-menu-english.png"));
        menu.IsOpen = false;
        Render(card, Path.Combine(pictures, "widget-english.png"));
        RenderPages(settings, pages, pictures, "english");
        if (OpenThemeLibrary(settings) is { } library)
        {
            Check(library.Title == "Theme library" && ThemeOption.Available(preferences, MiniInfos.Community.PackageLibrary.Shared)
                .Any(option => option.Key == ThemeHistory.Key(ThemeChoice.Classic) && option.Name == "Classic"),
                "the library translates its title and theme names while preserving the original Classic");
            Render(library, Path.Combine(pictures, "theme-library-english.png"));
            library.Close();
        }
        pages.SelectedIndex = 0;
        var example = new MascotExampleWindow { Owner = settings };
        example.Show();
        PumpUntil(() => example.Ready.IsCompleted, TimeSpan.FromSeconds(10));
        ChooseLanguage(LanguageChoice.Portuguese);
        Pump(TimeSpan.FromMilliseconds(100));
        Check(example.Title == "Exemplo de mascote", "an already open mascot example follows Portuguese");
        ChooseLanguage(LanguageChoice.English);
        Pump(TimeSpan.FromMilliseconds(100));
        var firstRow = (StackPanel)Find<StackPanel>(example, "RowLabels").Children[0];
        Check(example.Title == "Mascot example" && firstRow.Children.OfType<TextBlock>().Last().Text == "idle (required)",
            "the mascot example and cached state captions also return to English live");
        Render(example, Path.Combine(pictures, "mascot-example-english.png"));
        example.Close();
        CheckUpdateBanner(cards, preferences, pictures);
        ChooseLanguage(LanguageChoice.Automatic);
        Pump(TimeSpan.FromMilliseconds(100));
        Check(Localizer.Culture.Equals(Localizer.Resolve(LanguageChoice.Automatic, CultureInfo.CurrentUICulture)),
            "automatic restores detection of the Windows display language");
        ChooseLanguage(LanguageChoice.Portuguese);
        Pump(TimeSpan.FromMilliseconds(500));
        Check(settings.Title == "Configurações do MiniInfos" && (string)Find<Button>(settings, "ThemeLibraryButton").Content == "Biblioteca"
            && preferences.ThemeKey == theme, "Portuguese returns live, with the same theme");
        Render(settings, Path.Combine(pictures, "settings-portuguese.png"));

        void ChooseLanguage(LanguageChoice choice) => Find<StackPanel>(settings, "LanguageChoices").Children.OfType<RadioButton>()
            .Single(button => (string)button.Tag == choice.ToString()).IsChecked = true;
    }
}

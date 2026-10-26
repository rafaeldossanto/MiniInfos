namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckThemes(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        var simple = ThemeKeys(ThemeManager.Load(ThemeChoice.Simple));
        Check(simple.Length > 0 && new[] { ThemeChoice.Light, ThemeChoice.Classic }
            .All(theme => simple.SequenceEqual(ThemeKeys(ThemeManager.Load(theme)))),
            "all themes provide the same resources, including merged dictionaries");

        var card = cards.Main;
        var face = Find<Border>(card, "Card");
        var pages = Find<TabControl>(settings, "Pages");
        pages.SelectedIndex = 1;
        Find<RadioButton>(settings, "LightTheme").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(500));
        Check(IsLight(face.Background) && IsLight(((Border)settings.Content).Background) && SharedSettings().Contains("\"Theme\":\"Light\""),
            "Claro turns the card and the settings light, and is kept");
        Render(card, System.IO.Path.Combine(pictures, "card-light.png"));
        RenderPages(settings, pages, pictures, "light");
        var menu = face.ContextMenu!;
        menu.PlacementTarget = face;
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(IsLight(((Border)menu.Template.FindName("MenuSurface", menu)).Background), "and its menu");
        RenderElement(menu, System.IO.Path.Combine(pictures, "context-menu-light.png"));
        menu.IsOpen = false;

        pages.SelectedIndex = 1;
        Find<RadioButton>(settings, "SimpleTheme").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(500));
        Check(!IsLight(face.Background) && preferences.Theme == ThemeChoice.Simple, "Simples turns it dark again");

        var simpleHeight = card.Height;
        Find<RadioButton>(settings, "ClassicTheme").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(700));
        var artwork = Find<Image>(card, "AthenaArtwork");
        Check(preferences.Theme == ThemeChoice.Classic && artwork.Visibility == Visibility.Visible
            && artwork.Source is not null && card.Height > simpleHeight,
            "Clássico adds Athena and measures its header immediately");
        Check(Preferences.Load().Theme == ThemeChoice.Classic, "the classic choice is kept for the next launch");
        Render(card, System.IO.Path.Combine(pictures, "card-classic.png"));
        Render(settings, System.IO.Path.Combine(pictures, "settings-classic.png"));

        var icons = Find<StackPanel>(card, "PageIcons");
        var (_, split, _, _) = OpenPageMenu(icons, 1);
        Choose(split);
        var artCenter = artwork.TranslatePoint(new Point(artwork.ActualWidth / 2, 0), face).X;
        Check(Columns(card).Count == 2 && Math.Abs(artCenter - face.ActualWidth / 2) < 1,
            "in split view Athena stays centered over the whole card");
        Check(icons.Children.OfType<Button>().Count() == icons.Children.Count,
            "the provider icons have no separator between them");
        Render(card, System.IO.Path.Combine(pictures, "split-classic.png"));
        var (_, _, single, _) = OpenPageMenu(icons, 0);
        Choose(single);

        Find<RadioButton>(settings, "SimpleTheme").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(700));
        Check(artwork.Visibility == Visibility.Collapsed && Math.Abs(card.Height - simpleHeight) < 2,
            "Simples removes the illustration and restores its compact height");

    }

    private static string[] ThemeKeys(ResourceDictionary resources) => resources.Keys.Cast<object>()
        .Select(key => key.ToString()!)
        .Concat(resources.MergedDictionaries.SelectMany(ThemeKeys))
        .Distinct().Order().ToArray();

    private static void RenderPages(SettingsWindow settings, TabControl pages, string pictures, string theme)
    {
        for (var page = 0; page < pages.Items.Count; page++)
        {
            pages.SelectedIndex = page;
            Pump(TimeSpan.FromMilliseconds(300));
            Render(settings, System.IO.Path.Combine(pictures, $"settings-{page + 1}-{theme}.png"));
        }

        pages.SelectedIndex = 0;
        Pump(TimeSpan.FromMilliseconds(300));
    }

    private static bool IsLight(Brush brush) => brush is SolidColorBrush { Color.R: > 0xC0 };
}

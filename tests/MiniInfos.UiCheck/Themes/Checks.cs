using MiniInfos.Community;

namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckThemes(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        var simple = ThemeKeys(ThemeManager.Load(ThemeChoice.Simple));
        Check(simple.Length > 0 && new[] { ThemeChoice.Light, ThemeChoice.Classic, ThemeChoice.ClassicLight }
            .All(theme => simple.SequenceEqual(ThemeKeys(ThemeManager.Load(theme)))),
            "all themes provide the same resources, including merged dictionaries");

        var card = cards.Main;
        var face = Find<Border>(card, "Card");
        var pages = Find<TabControl>(settings, "Pages");
        pages.SelectedIndex = 1;
        ChooseTheme(settings, ThemeChoice.Light);
        Pump(TimeSpan.FromMilliseconds(500));
        Check(IsLight(face.Background) && IsLight(((Border)settings.Content).Background) && SharedSettings().Contains("\"Theme\":\"Light\""),
            "Claro turns the card and the settings light, and is kept");
        var light = ThemeHistory.Key(ThemeChoice.Light);
        Check(RowKeys(settings) is [var first, _, _, _] && first == light && Preferences.Load().RecentThemes[0] == light,
            "the theme chosen last moves to the front of the row, and stays there for the next launch");
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
        ChooseTheme(settings, ThemeChoice.Simple);
        Pump(TimeSpan.FromMilliseconds(500));
        Check(!IsLight(face.Background) && preferences.Theme == ThemeChoice.Simple, "Simples turns it dark again");

        var simpleHeight = card.Height;
        ChooseTheme(settings, ThemeChoice.Classic);
        Pump(TimeSpan.FromMilliseconds(700));
        var artwork = Find<Image>(card, "AthenaArtwork");
        Check(preferences.Theme == ThemeChoice.Classic && artwork.Visibility == Visibility.Visible
            && artwork.Source is not null && card.Height > simpleHeight,
            "Clássico adds Athena and measures its header immediately");
        Check(Preferences.Load().Theme == ThemeChoice.Classic, "the classic choice is kept for the next launch");
        var band = Find<Grid>(card, "HeaderArt");
        var refresh = Find<Button>(card, "RefreshButton");
        Check(band.TranslatePoint(new Point(), face).Y >= refresh.TranslatePoint(new Point(0, refresh.ActualHeight), face).Y,
            "the Athena band starts below the controls without covering them");
        var engraved = artwork.Source;
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

        ChooseTheme(settings, ThemeChoice.ClassicLight);
        Pump(TimeSpan.FromMilliseconds(500));
        Check(IsLight(face.Background) && artwork.Visibility == Visibility.Visible && artwork.Source is BitmapSource
            && !Equals(engraved, artwork.Source) && Preferences.Load().Theme == ThemeChoice.ClassicLight,
            "Clássico claro uses its own marble artwork and saves the light classical choice");
        Check(Columns(card).Count == 2
            && Math.Abs(artwork.TranslatePoint(new Point(artwork.ActualWidth / 2, 0), face).X - face.ActualWidth / 2) < 1,
            "the marble Athena also stays centered across split view");
        Render(card, System.IO.Path.Combine(pictures, "split-classic-light.png"));
        var (_, _, single, _) = OpenPageMenu(icons, 0);
        Choose(single);
        Render(card, System.IO.Path.Combine(pictures, "card-classic-light.png"));

        ChooseTheme(settings, ThemeChoice.Simple);
        Pump(TimeSpan.FromMilliseconds(700));
        Check(artwork.Visibility == Visibility.Collapsed && Math.Abs(card.Height - simpleHeight) < 2,
            "Simples removes the illustration and restores its compact height");

        CheckThemeLibrary(preferences, settings, pictures);
        ChooseTheme(settings, ThemeChoice.Simple);
    }

    /// <summary>Biblioteca: every theme on a card with its preview; Usar applies one and puts it first in the row.</summary>
    private static void CheckThemeLibrary(Preferences preferences, SettingsWindow settings, string pictures)
    {
        var button = Find<Button>(settings, "ThemeLibraryButton");
        Check(button.Content as string == "Biblioteca" && button.IsVisible && ((Panel)button.Parent).Children.IndexOf(button) == 0,
            "Biblioteca is the first button under the theme row");
        if (OpenThemeLibrary(settings) is not { } library) return;

        try
        {
            var uses = LibraryUses(library);
            Check(uses.Count >= 5 && uses.All(use => LibraryPreview(use) is BitmapSource { PixelWidth: PackagePreviews.Size, IsFrozen: true }),
                $"the library shows every theme with its preview ({uses.Count})");
            Check(uses[0] is { IsEnabled: false, Content: "Em uso" } && (string)uses[0].Tag == preferences.ThemeKey
                && uses.Skip(1).All(use => use is { IsEnabled: true, Content: "Usar" }),
                "the theme in use comes first and reads Em uso; the others offer Usar");
            Render(library, System.IO.Path.Combine(pictures, "theme-library.png"));

            var order = uses.Select(use => (string)use.Tag).ToArray();
            var chosen = uses[^1];
            chosen.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(500));
            Check(preferences.ThemeKey == (string)chosen.Tag && chosen is { IsEnabled: false, Content: "Em uso" }
                && LibraryUses(library).Select(use => (string)use.Tag).SequenceEqual(order),
                "Usar applies the theme, and the cards keep their places while the library is open");
            Check(RowKeys(settings).FirstOrDefault() == (string)chosen.Tag && Preferences.Load().RecentThemes[0] == (string)chosen.Tag,
                "the theme used from the library leads the Aparência row");

            Find<Button>(settings, "ThemeLibraryButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(200));
            Check(settings.OwnedWindows.OfType<ThemeLibraryWindow>().Count() == 1, "asked again, the same library comes forward");

            var title = (FrameworkElement)library.Template.FindName("PART_TitleBar", library);
            Check(title.IsVisible && ((Panel)title).Children.OfType<TextBlock>().Single().Text == "Biblioteca de temas",
                "the library wears the settings' frame, with its title");
            ((Button)library.Template.FindName("PART_Close", library)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(200));
            Check(!library.IsVisible, "and its ✕ closes it");

            if (OpenThemeLibrary(settings) is not { } reopened) return;
            library = reopened;
            Find<Button>(library, "CommunityButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(300));
            Check(!library.IsVisible && Find<TabControl>(settings, "Pages").SelectedItem == Find<TabItem>(settings, "CommunityPage")
                && Find<RadioButton>(settings, "CatalogThemes").IsChecked == true,
                "Mais temas na Comunidade opens the Comunidade page on its themes");
            Find<RadioButton>(settings, "CatalogMascots").IsChecked = true;
            Find<TabControl>(settings, "Pages").SelectedIndex = 1;
        }
        finally
        {
            library.Close();
            Pump(TimeSpan.FromMilliseconds(100));
        }
    }

    /// <summary>The theme keys in the Tema row, in its order.</summary>
    private static string[] RowKeys(SettingsWindow settings) =>
        [.. Find<StackPanel>(settings, "ThemeChoices").Children.OfType<RadioButton>().Select(choice => (string)choice.Tag)];

    /// <summary>Clicks a built-in theme in the Tema row, as the user would.</summary>
    private static void ChooseTheme(SettingsWindow settings, ThemeChoice theme)
    {
        var key = ThemeHistory.Key(theme);
        var choice = Find<StackPanel>(settings, "ThemeChoices").Children.OfType<RadioButton>()
            .FirstOrDefault(button => (string)button.Tag == key);
        if (choice is not null) choice.IsChecked = true;
        else UseInLibrary(settings, key);
        Check(RowKeys(settings).FirstOrDefault() == key, $"choosing {theme} puts it first in the Tema row");
    }

    private static ThemeLibraryWindow? OpenThemeLibrary(SettingsWindow settings)
    {
        Find<Button>(settings, "ThemeLibraryButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        PumpUntil(() => settings.OwnedWindows.OfType<ThemeLibraryWindow>().Any(), TimeSpan.FromSeconds(5));
        var library = settings.OwnedWindows.OfType<ThemeLibraryWindow>().FirstOrDefault();
        Check(library is { IsVisible: true }, "Biblioteca opens a window above the settings");
        if (library is not null) PumpUntil(() => library.Ready.IsCompleted, TimeSpan.FromSeconds(10));
        return library;
    }

    /// <summary>Each card's Usar button, in the library's order; its Tag is the theme's key.</summary>
    private static List<Button> LibraryUses(ThemeLibraryWindow library) =>
        [.. Find<WrapPanel>(library, "Tiles").Children.OfType<Border>().SelectMany(card => ((StackPanel)card.Child).Children.OfType<Button>())];

    private static ImageSource? LibraryPreview(Button use) => ((StackPanel)use.Parent).Children.OfType<Image>().Single().Source;

    /// <summary>Uses a theme from the library, as its Usar button does, and closes it.</summary>
    private static void UseInLibrary(SettingsWindow settings, string key)
    {
        if (OpenThemeLibrary(settings) is not { } library) return;

        try
        {
            var use = LibraryUses(library).FirstOrDefault(button => (string)button.Tag == key);
            Check(use is not null && LibraryPreview(use) is BitmapSource, $"the library offers {key} with its preview");
            use?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(350));
        }
        finally
        {
            library.Close();
            Pump(TimeSpan.FromMilliseconds(100));
        }
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

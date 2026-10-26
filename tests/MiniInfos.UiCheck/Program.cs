using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MiniInfos;

/// <summary>
/// Opens the real card with the real providers, its data kept apart in a temporary folder, and
/// checks what only a live window shows. Usage: MiniInfos.UiCheck [folder for the PNGs].
/// </summary>
internal static partial class Program
{
    private const int WmNcHitTest = 0x0084;
    private const int WmSizing = 0x0214;
    private const int WmMouseHorizontalWheel = 0x020E;
    private const int HtLeft = 10;
    private const int HtBottom = 15;
    private const int HtBottomRight = 17;
    private const int HtClient = 1;

    private static int _failures;

    [STAThread]
    private static int Main(string[] args)
    {
        var data = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MiniInfos.UiCheck-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("MINIINFOS_DATA_DIR", data);
        Environment.SetEnvironmentVariable("MINIINFOS_STARTUP_DIR", System.IO.Path.Combine(data, "startup"));
        var pictures = args.FirstOrDefault() ?? data;
        Directory.CreateDirectory(pictures);

        try
        {
            Run(pictures);
        }
        finally
        {
            if (args.Length > 0 && Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }

        Console.WriteLine(_failures == 0 ? "ALL OK" : $"{_failures} FAILED");
        return _failures;
    }

    private static void Run(string pictures)
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var preferences = Preferences.Load();
        Themes.Follow(preferences);
        AppFonts.Follow(preferences);
        var providers = UsageProviders.Create();
        var cards = new CardSet(providers, preferences);
        var window = cards.Main;
        var summaries = new List<string>();
        window.SummaryChanged += summaries.Add;
        using var tray = new TrayIcon(cards);

        var opening = Stopwatch.StartNew();
        window.Show();
        PumpUntil(() => window.Opacity == 1, TimeSpan.FromSeconds(10));

        // The data folder starts empty, so the first read goes through the whole history, as on a first launch:
        // the card must not wait for it.
        var shownAt = opening.Elapsed;
        var shownHeight = window.Height;
        var firstPage = Columns(window)[0];
        var wasReading = Find<TextBlock>(firstPage, "Reading").Visibility == Visibility.Visible;
        Check(window.Opacity == 1 && shownAt < TimeSpan.FromSeconds(2),
            $"the card shows at once, its first read {(wasReading ? "still going" : "already done")} ({shownAt.TotalSeconds:0.0} s)");
        PumpUntil(() => Find<StackPanel>(firstPage, "Numbers").Visibility == Visibility.Visible, TimeSpan.FromSeconds(60));
        Check(Find<StackPanel>(firstPage, "Numbers").Visibility == Visibility.Visible
            && Find<TextBlock>(firstPage, "Reading").Visibility == Visibility.Collapsed,
            $"the numbers fill in once the history is read ({opening.Elapsed.TotalSeconds:0.0} s)");
        Check(Math.Abs(window.Height - shownHeight) < 2, $"and the card keeps its size ({shownHeight:0} -> {window.Height:0})");
        Pump(TimeSpan.FromSeconds(1));
        var handle = new WindowInteropHelper(window).Handle;

        CheckEdges(handle);
        RenderMenus(window, pictures);
        CheckTray(tray, summaries, window);
        CheckViews(window);
        CheckRefresh(window, summaries);
        CheckPages(window, handle, providers.Count, pictures);
        CheckSplit(window, handle, pictures);
        CheckNewWindow(cards, pictures);
        CheckSettings(cards, preferences, pictures);
        CheckUpdateBanner(cards, preferences, pictures);
        CheckApps();
        CheckDragKeepsProportions(window, handle, "one page");

        cards.Quit();
    }

    /// <summary>Popups have their own visuals: capture them as well as the card for theme review.</summary>
    private static void RenderMenus(MainWindow window, string pictures)
    {
        var card = Find<Border>(window, "Card");
        var menu = card.ContextMenu!;
        menu.PlacementTarget = card;
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        RenderElement(menu, System.IO.Path.Combine(pictures, "context-menu.png"));
        menu.IsOpen = false;

        var (openIn, _, _, _) = OpenPageMenu(Find<StackPanel>(window, "PageIcons"), 1);
        openIn.IsSubmenuOpen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        RenderElement((ContextMenu)openIn.Parent, System.IO.Path.Combine(pictures, "icon-menu.png"));
        var popup = (Popup)openIn.Template.FindName("PART_Popup", openIn);
        RenderElement((FrameworkElement)popup.Child, System.IO.Path.Combine(pictures, "icon-submenu.png"));
        openIn.IsSubmenuOpen = false;
        ((ContextMenu)openIn.Parent).IsOpen = false;
    }

    private static void CheckSplit(MainWindow window, IntPtr handle, string pictures)
    {
        var icons = Find<StackPanel>(window, "PageIcons");
        var singleWidth = window.Width;

        var (openIn, split, single, _) = OpenPageMenu(icons, 1);
        Check(openIn.Header as string == "Abrir em" && Offered(split) && !Offered(single),
            "right-clicking a page icon offers Abrir em > Visualização dividida");
        Choose(split);

        Check(Columns(window).Count == 2, "Visualização dividida shows two pages side by side");
        Check(IconIsLit(icons, 0) && IconIsLit(icons, 1), "and lights both their icons");
        Check(Find<TextBlock>(Columns(window)[0], "PageTitle").Text != Find<TextBlock>(Columns(window)[1], "PageTitle").Text,
            $"each column shows its own provider ({Find<TextBlock>(Columns(window)[0], "PageTitle").Text} | {Find<TextBlock>(Columns(window)[1], "PageTitle").Text})");
        Check(Find<TextBlock>(Columns(window)[0], "Note").Text != "" && Find<TextBlock>(Columns(window)[1], "Note").Text != "",
            "each column says how fresh its own limits are");
        Check(window.Width > singleWidth * 1.8 && window.Width < singleWidth * 2.1, $"the card widens to hold them ({singleWidth:0} -> {window.Width:0})");
        Check(window.Left >= SystemParameters.VirtualScreenLeft
            && window.Left + window.Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth, "and stays on screen");
        Check(Settings().Contains("\"BesidePage\":\"codex\""), "the split is kept for next time");
        CheckDragKeepsProportions(window, handle, "split");
        Render(window, System.IO.Path.Combine(pictures, "split.png"));

        var (_, splitAgain, back, _) = OpenPageMenu(icons, 0);
        Check(Offered(back) && !Offered(splitAgain), "a split card offers Visualização única");
        Choose(back);
        Check(Columns(window).Count == 1 && IconIsLit(icons, 0) && !IconIsLit(icons, 1), "Visualização única goes back to one page");
        Check(Math.Abs(window.Width - singleWidth) < 2, $"at the card's own width ({window.Width:0})");
    }

    private static void CheckNewWindow(CardSet cards, string pictures)
    {
        var (_, _, _, newWindow) = OpenPageMenu(Find<StackPanel>(cards.Main, "PageIcons"), 1);
        Choose(newWindow);
        PumpUntil(() => cards.Extras.Count == 1 && cards.Extras[0].Opacity == 1, TimeSpan.FromSeconds(10));

        var extra = cards.Extras.FirstOrDefault();
        Check(extra is { IsVisible: true }, "Nova janela opens a card of its own");
        if (extra is null) return;

        Pump(TimeSpan.FromSeconds(1));
        Check(Find<TextBlock>(Columns(extra)[0], "PageTitle").Text == "Codex (GPT)", "showing the page it was opened from");
        Check(Directory.EnumerateFiles(System.IO.Path.Combine(Data, "windows"), "*.json").Count() == 1, "and kept, so it comes back with the app");
        Check((Find<Button>(extra, "DismissButton").ToolTip as string) == "Fechar esta janela", "its ✕ closes it");
        var mainIcons = Find<StackPanel>(cards.Main, "PageIcons");
        var extraIcons = Find<StackPanel>(extra, "PageIcons");
        Check(IconIsLit(mainIcons, 0) && IconIsLit(mainIcons, 1) && IconIsLit(extraIcons, 0) && IconIsLit(extraIcons, 1),
            "with Claude in one card and Codex in the other, both pagers light both icons");
        Render(extra, System.IO.Path.Combine(pictures, "new-window.png"));

        ((Button)extraIcons.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        Check(!IconIsLit(mainIcons, 1) && IconIsLit(extraIcons, 0) && !IconIsLit(extraIcons, 1),
            "when both cards show Claude, Codex's icon goes dark in both");
        ((Button)extraIcons.Children[1]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));

        cards.Hide();
        Check(!extra.IsVisible && !cards.Main.IsVisible, "hiding the cards hides it too");
        cards.BringBack();
        Pump(TimeSpan.FromSeconds(1));
        Check(extra.IsVisible && cards.Main.IsVisible, "and bringing them back brings it back");

        Find<Button>(extra, "DismissButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        Check(cards.Extras.Count == 0 && !Directory.EnumerateFiles(System.IO.Path.Combine(Data, "windows"), "*.json").Any(),
            "closing it forgets it for good");
        Check(IconIsLit(mainIcons, 0) && !IconIsLit(mainIcons, 1), "and its page's icon goes dark in the main card");
    }

    /// <summary>A newer version, served by fakes: the line above the card, and a click that installs it.</summary>
    private static void CheckUpdateBanner(CardSet cards, Preferences preferences, string pictures)
    {
        var card = cards.Main;
        var bytes = System.Text.Encoding.ASCII.GetBytes("MZ fake installer");
        var update = new AvailableUpdate(new Version(99, 0, 0), new Uri("https://github.com/owner/MiniInfos/releases/download/v99.0.0/MiniInfos-Setup.exe"),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        string? launched = null;
        using var updater = new Updater(new FakeSource(update), new System.Net.Http.HttpClient(new FakeDownload(bytes)),
            new Version(AppInfo.Version), path => launched = path);
        var banner = new UpdateBanner(card, updater, preferences);
        card.Top = SystemParameters.WorkArea.Bottom - card.ActualHeight - 16;

        Check(!banner.IsVisible, "with nothing newer, no line shows");
        _ = updater.CheckAsync();
        PumpUntil(() => banner.IsVisible, TimeSpan.FromSeconds(5));
        Pump(TimeSpan.FromMilliseconds(300));
        var message = Shown(Find<TextBlock>(banner, "Message"));
        var button = Find<Button>(banner, "DownloadButton");
        Check(banner.IsVisible && message == "Nova atualização disponível" && button.IsEnabled,
            $"a newer release shows the line with its button (\"{message}\", button {(button.IsEnabled ? "on" : "off")})");
        Check(banner.Top + banner.ActualHeight <= card.Top && Math.Abs(banner.Left - card.Left) < 1 && Math.Abs(banner.ActualWidth - card.ActualWidth) < 1,
            $"in a window of its own, just above the card and as wide ({banner.Left:0},{banner.Top:0} {banner.ActualWidth:0} over {card.Left:0},{card.Top:0} {card.ActualWidth:0})");
        Render(banner, System.IO.Path.Combine(pictures, "update-banner.png"));

        var bottom = card.Top;
        card.Top = SystemParameters.VirtualScreenTop;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(banner.Top >= card.Top + card.ActualHeight, "with no room above, it goes below the card");
        card.Top = bottom;
        Pump(TimeSpan.FromMilliseconds(300));

        card.HideCard();
        Pump(TimeSpan.FromMilliseconds(300));
        Check(!banner.IsVisible, "it hides with the card");
        card.BringBack();
        Pump(TimeSpan.FromMilliseconds(300));
        Check(banner.IsVisible, "and comes back with it");

        Find<Border>(banner, "Strip").RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        PumpUntil(() => launched is not null, TimeSpan.FromSeconds(10));
        Check(launched is not null && File.ReadAllBytes(launched).SequenceEqual(bytes) && updater.Stage == UpdateStage.Installing,
            "clicking the line downloads the installer, checks it and runs it");
        Pump(TimeSpan.FromMilliseconds(200));
        Check(Shown(Find<TextBlock>(banner, "Message")).StartsWith("Instalando") && !Find<Button>(banner, "DownloadButton").IsEnabled,
            "while the installer runs, the line says so and the button rests");
        if (launched is not null) File.Delete(launched);
        banner.Close();
    }

    /// <summary>What a text block draws, its runs included, read from its content rather than its Text property.</summary>
    private static string Shown(TextBlock text) => new TextRange(text.ContentStart, text.ContentEnd).Text;

    private sealed class FakeSource(AvailableUpdate update) : IUpdateSource
    {
        public Task<AvailableUpdate?> NewerThanAsync(Version current, CancellationToken cancellation) => Task.FromResult<AvailableUpdate?>(update);
    }

    private sealed class FakeDownload(byte[] body) : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(body) });
    }

    private static void CheckSettings(CardSet cards, Preferences preferences, string pictures)
    {
        var simple = ThemeKeys(Themes.Load(ThemeChoice.Simple));
        Check(simple.Length > 0 && new[] { ThemeChoice.Light, ThemeChoice.Classic }
            .All(theme => simple.SequenceEqual(ThemeKeys(Themes.Load(theme)))),
            "all themes provide the same resources, including merged dictionaries");

        var card = cards.Main;
        var face = Find<Border>(card, "Card");
        var gear = Find<Button>(card, "SettingsButton");
        var at = gear.TranslatePoint(new Point(0, 0), face);
        Check(at.X < 24 && at.Y + gear.ActualHeight > face.ActualHeight - 16,
            $"the gear sits in the card's bottom-left corner ({at.X:0},{at.Y:0} in {face.ActualWidth:0}x{face.ActualHeight:0})");

        gear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        PumpUntil(() => cards.Settings is { IsVisible: true }, TimeSpan.FromSeconds(5));
        var settings = cards.Settings;
        Check(settings is { IsVisible: true } && settings.Card == card, "the gear opens Configurações for its card");
        if (settings is null) return;

        Pump(TimeSpan.FromMilliseconds(500));
        gear.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Settings == settings, "pressed again, the same window comes forward");

        var pages = Find<TabControl>(settings, "Pages");
        var names = pages.Items.OfType<TabItem>().Select(page => page.Header as string).ToList();
        Check(names.SequenceEqual(["Geral", "Aparência", "Mascote", "Sobre"]), $"with its pages ({string.Join(", ", names)})");
        RenderPages(settings, pages, pictures, "dark");

        var topmost = Find<CheckBox>(settings, "TopmostToggle");
        topmost.IsChecked = false;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(!preferences.IsTopmost && !card.Topmost && !Find<MenuItem>(card, "TopmostItem").IsChecked && SharedSettings().Contains("\"IsTopmost\":false"),
            "Sempre por cima off lets the card fall behind, unticks its menu item and is kept");
        topmost.IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.Topmost && Find<MenuItem>(card, "TopmostItem").IsChecked, "back on, it stays in front again");

        var follow = Find<CheckBox>(settings, "FollowToggle");
        follow.IsChecked = false;
        Check(!preferences.FollowsApps && SharedSettings().Contains("\"FollowsApps\":false"), "Acompanhar o Claude e o Codex off is kept");
        follow.IsChecked = true;

        var shortcut = System.IO.Path.Combine(Environment.GetEnvironmentVariable("MINIINFOS_STARTUP_DIR")!, "MiniInfos.lnk");
        var startup = Find<CheckBox>(settings, "StartupToggle");
        startup.IsChecked = true;
        Check(File.Exists(shortcut) && startup.IsChecked == true, "Iniciar com o Windows makes the startup shortcut");
        startup.IsChecked = false;
        Check(!File.Exists(shortcut), "and off, takes it away");

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
        CheckFonts(cards, preferences, settings, pictures);

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

        Find<RadioButton>(settings, "DetailedNumbers").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.IsDetailed && Find<RadioButton>(card, "DetailedView").IsChecked == true, "Números: Detalhado flips the card's own switch");
        Find<RadioButton>(card, "CompactView").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(Find<RadioButton>(settings, "CompactNumbers").IsChecked == true, "and the card's switch shows in the settings");

        CheckPet(cards, preferences, settings, pictures);
        Find<Button>(settings, "CloseButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Settings is null && !settings.IsVisible, "its close button closes it");
    }

    private static void CheckPet(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        Check(cards.Pet is null && !preferences.PetEnabled, "the desktop pet is optional and stays unloaded while disabled");
        settings.ShowPetPage();
        Find<CheckBox>(settings, "PetToggle").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(500));
        var pet = cards.Pet;
        Check(pet is { IsVisible: true, AllowsTransparency: true, ShowInTaskbar: false, Owner: null },
            "enabling the owl opens an independent transparent desktop window");
        if (pet is null) return;
        Check(Preferences.Load().PetEnabled, "the enabled choice is saved");
        Check(Enum.GetValues<PetState>().All(state => Enumerable.Range(0, PetSprites.FrameCount).All(index =>
        {
            var frame = PetSprites.Frame(state, index);
            return frame.Source.IsFrozen && !frame.Hits(new Point(0, 0)) && frame.Hits(new Point(160, 200));
        })), "every animation frame is present, grounded and transparent outside the owl");
        var petHandle = new WindowInteropHelper(pet).Handle;
        var corner = pet.PointToScreen(new Point(2, 2));
        var point = new IntPtr(((int)corner.Y << 16) | ((int)corner.X & 0xFFFF));
        Check(SendMessage(petHandle, WmNcHitTest, IntPtr.Zero, point).ToInt64() == -1,
            "clicks in the transparent padding pass through to the desktop");
        cards.Hide();
        Check(pet.IsVisible && !cards.Main.IsVisible, "hiding the card leaves the owl available on the desktop");
        var sprite = Find<Image>(pet, "Sprite");
        var positionBeforeBreath = new Point(pet.Left, pet.Top);
        var beforeBreath = sprite.RenderTransform.Value;
        Pump(TimeSpan.FromMilliseconds(200));
        Check(sprite.RenderTransform.Value != beforeBreath && new Point(pet.Left, pet.Top) == positionBeforeBreath,
            "the owl breathes continuously between sprite frames without moving its desktop window");
        var menu = sprite.ContextMenu!;
        menu.PlacementTarget = sprite;
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(200));
        var show = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Mostrar MiniInfos");
        show.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.IsOpen = false;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Main.IsVisible, "the owl's menu brings the card back");
        Find<RadioButton>(settings, "LargePet").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(200));
        Check(pet.Width == PetPlacement.Size(PetSizeChoice.Large) && Preferences.Load().PetSize == PetSizeChoice.Large,
            "the size setting updates the pet immediately and is saved");
        pet.Left = SystemParameters.WorkArea.Left + 80;
        pet.Top = SystemParameters.WorkArea.Top + 100;
        var moved = new Point(pet.Left, pet.Top);
        Render(pet, System.IO.Path.Combine(pictures, "desktop-owl.png"));
        Render(settings, System.IO.Path.Combine(pictures, "settings-pet.png"));
        Find<CheckBox>(settings, "PetToggle").IsChecked = false;
        Check(cards.Pet is null && !pet.IsVisible && !Preferences.Load().PetEnabled,
            "turning it off closes the window and saves the choice");
        Find<CheckBox>(settings, "PetToggle").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(cards.Pet is { IsVisible: true } restored && Math.Abs(restored.Left - moved.X) < 1 && Math.Abs(restored.Top - moved.Y) < 1,
            "the owl returns to its saved desktop position");
        preferences.IsTopmost = false;
        Check(cards.Pet?.Topmost == false, "the owl follows Sempre por cima too");
        preferences.IsTopmost = true;
        Find<CheckBox>(settings, "PetToggle").IsChecked = false;
        Find<RadioButton>(settings, "MediumPet").IsChecked = true;
    }

    private static void CheckFonts(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        var card = cards.Main;
        foreach (var option in AppFonts.TextOptions.Concat(AppFonts.NumberOptions).DistinctBy(option => option.Choice))
        {
            foreach (var weight in new[] { FontWeights.Normal, FontWeights.SemiBold })
            {
                var typeface = new Typeface(option.Family, FontStyles.Normal, weight, FontStretches.Normal);
                Check(typeface.TryGetGlyphTypeface(out var glyph) && glyph.FontUri.Scheme == "pack"
                    && glyph.FamilyNames.Values.Contains(option.Name), $"{option.Name} {weight} loads from the app, without font fallback");
            }
        }

        void ChooseFont(string panel, FontChoice choice) => Find<StackPanel>(settings, panel).Children
            .OfType<RadioButton>().Single(button => (FontChoice)button.Tag == choice).IsChecked = true;

        var (_, _, _, open) = OpenPageMenu(Find<StackPanel>(card, "PageIcons"), 1);
        Choose(open);
        PumpUntil(() => cards.Extras.Count == 1 && cards.Extras[0].Opacity == 1, TimeSpan.FromSeconds(10));
        var extra = cards.Extras.Single();
        var originalWidth = card.Width;
        card.IsDetailed = true;
        var combinations = new[]
        {
            (Name: "atual", Text: FontChoice.Manrope, Number: FontChoice.Manrope),
            (Name: "monoespacada", Text: FontChoice.Manrope, Number: FontChoice.GeistMono),
            (Name: "monumental", Text: FontChoice.Cinzel, Number: FontChoice.BodoniModa),
            (Name: "digital", Text: FontChoice.ChakraPetch, Number: FontChoice.Orbitron),
        };
        foreach (var combination in combinations)
        {
            ChooseFont("TextFontChoices", combination.Text);
            ChooseFont("NumberFontChoices", combination.Number);
            Pump(TimeSpan.FromMilliseconds(500));
            var page = Columns(card)[0];
            var detail = Find<TextBlock>(page, "ShortDetail");
            Check(card.FontFamily == AppFonts.Family(combination.Text)
                && extra.FontFamily == card.FontFamily
                && Find<TextBlock>(page, "TodayTokens").FontFamily == AppFonts.Family(combination.Number)
                && Find<TextBlock>(Columns(extra)[0], "TodayTokens").FontFamily == AppFonts.Family(combination.Number),
                $"{combination.Name}: both open cards take the chosen text and number fonts immediately");
            Check(detail.Inlines.OfType<Run>().Any(run => run.Text.Any(char.IsDigit)
                    && run.FontFamily == AppFonts.Family(combination.Number))
                && detail.Inlines.OfType<Run>().Any(run => run.Text.Contains("tokens")
                    && run.FontFamily == AppFonts.Family(combination.Text)),
                "mixed captions keep letters and digits in their separate families");
            var saved = Preferences.Load();
            Check(saved.TextFont == combination.Text && saved.NumberFont == combination.Number,
                "the two font choices are saved independently");
            Check(Math.Abs(card.Width - originalWidth) < 1, "changing fonts preserves the card's scale");
            var smallCaps = combination.Text == FontChoice.Cinzel;
            Check(Find<TextBlock>(card, "AppTitle").Text == (smallCaps ? "Miniinfos" : "MiniInfos")
                && Find<TextBlock>(Columns(extra)[0], "PageTitle").Text == (smallCaps ? "Codex (Gpt)" : "Codex (GPT)"),
                smallCaps ? "with Cinzel only the capitals that open a word stay (Miniinfos, Codex (Gpt))" : "other text fonts keep the names as written");
            Render(card, System.IO.Path.Combine(pictures, $"font-{combination.Name}.png"));
        }

        Render(settings, System.IO.Path.Combine(pictures, "settings-fonts.png"));
        var (_, split, _, _) = OpenPageMenu(Find<StackPanel>(card, "PageIcons"), 1);
        Choose(split);
        Check(Columns(card).All(page => Find<TextBlock>(page, "TodayTokens").FontFamily == AppFonts.Family(FontChoice.Orbitron)),
            "both split columns use the selected number font");
        Render(card, System.IO.Path.Combine(pictures, "split-digital.png"));
        Find<RadioButton>(settings, "LightTheme").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.FontFamily == AppFonts.Family(FontChoice.ChakraPetch)
            && Find<TextBlock>(Columns(card)[0], "TodayTokens").FontFamily == AppFonts.Family(FontChoice.Orbitron),
            "changing the color theme leaves the font choices intact");
        Find<RadioButton>(settings, "ClassicTheme").IsChecked = true;
        var (_, _, single, _) = OpenPageMenu(Find<StackPanel>(card, "PageIcons"), 0);
        Choose(single);
        Find<Button>(extra, "DismissButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ChooseFont("TextFontChoices", FontChoice.Manrope);
        ChooseFont("NumberFontChoices", FontChoice.Manrope);
        Pump(TimeSpan.FromMilliseconds(500));
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

    /// <summary>Opens a page icon's right-click menu as Windows would, and hands back its items.</summary>
    private static (MenuItem OpenIn, MenuItem Split, MenuItem Single, MenuItem NewWindow) OpenPageMenu(StackPanel icons, int page)
    {
        var button = (Button)icons.Children[page];
        var menu = button.ContextMenu!;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
        Pump(TimeSpan.FromMilliseconds(300));
        var openIn = (MenuItem)menu.Items[0];
        var items = openIn.Items.OfType<MenuItem>().ToList();
        return (openIn, items.Single(item => item.Header as string == "Visualização dividida"),
            items.Single(item => item.Header as string == "Visualização única"),
            items.Single(item => item.Header as string == "Nova janela"));
    }

    /// <summary>Whether the menu offers an item: what it sets as it opens, read without drawing the submenu.</summary>
    private static bool Offered(MenuItem item) => item.Visibility == Visibility.Visible;

    private static void Choose(MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (item.Parent is MenuItem { Parent: ContextMenu menu }) menu.IsOpen = false;
        Pump(TimeSpan.FromSeconds(2));
    }

    private static List<PageView> Columns(MainWindow window) => [.. Find<StackPanel>(window, "Columns").Children.OfType<PageView>()];

    private static bool IconIsLit(StackPanel icons, int page) =>
        ((System.Windows.Shapes.Path)((Button)icons.Children[page]).Content).Fill is SolidColorBrush { Color: { R: 0xED, G: 0xED, B: 0xED } };

    private static string Data => Environment.GetEnvironmentVariable("MINIINFOS_DATA_DIR")!;

    private static void CheckEdges(IntPtr handle)
    {
        var rect = WindowRect(handle);
        int Hit(int x, int y) => (int)SendMessage(handle, WmNcHitTest, IntPtr.Zero, (y << 16) | (x & 0xFFFF));
        Check(Hit(rect.Right - 3, rect.Bottom - 3) == HtBottomRight, "the bottom-right corner resizes");
        Check(Hit(rect.Left + 2, rect.Top + rect.Height / 2) == HtLeft, "the left edge resizes");
        Check(Hit(rect.Left + rect.Width / 2, rect.Bottom - 2) == HtBottom, "the bottom edge resizes");
        Check(Hit(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2) == HtClient, "the middle is the card, to drag it in any direction");
    }

    private static void CheckTray(TrayIcon tray, List<string> summaries, MainWindow window)
    {
        var icon = (System.Windows.Forms.NotifyIcon)typeof(TrayIcon)
            .GetField("_icon", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tray)!;
        Check(icon.Visible && icon.Icon is { Width: > 0 }, "the tray icon is up");
        Check(summaries.Count > 0 && icon.Text == summaries[^1], "hovering it reads the latest numbers");
        Check(Regex.IsMatch(icon.Text, @"Hoje [0-9]+(,[0-9])?[kMB]? tokens"), $"and their short form ({Line(icon.Text)})");
        Check(icon.ContextMenuStrip!.Items.Cast<System.Windows.Forms.ToolStripItem>().Any(item => item.Text == "Configurações"),
            "its menu opens Configurações");

        window.HideCard();
        var before = summaries.Count;
        Pump(TimeSpan.FromSeconds(4));
        Check(summaries.Count > before, "the numbers keep coming while the card is hidden");
        window.BringBack();
        Pump(TimeSpan.FromSeconds(1));
        Check(window.IsVisible, "Mostrar brings the card back");
    }

    private static void CheckViews(MainWindow window)
    {
        var total = Find<TextBlock>(Columns(window)[0], "TotalTokens");
        Find<RadioButton>(window, "CompactView").IsChecked = true;
        Pump(TimeSpan.FromSeconds(2));
        Check(Regex.IsMatch(total.Text, "^[0-9]+(,[0-9])?[kMB]?$"), $"Resumido shows the short form ({total.Text})");

        Find<RadioButton>(window, "DetailedView").IsChecked = true;
        Pump(TimeSpan.FromSeconds(2));
        Check(Regex.IsMatch(total.Text, @"^[0-9]{1,3}(\.[0-9]{3})*$"), $"Detalhado shows every digit ({total.Text})");
        Check(Settings().Contains("\"IsDetailed\":true"), "the view is kept for next time");
    }

    private static void CheckRefresh(MainWindow window, List<string> summaries)
    {
        var refresh = Find<Button>(window, "RefreshButton");
        var spin = Find<RotateTransform>(window, "RefreshSpin");
        var before = summaries.Count;
        refresh.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(250));
        Check(!refresh.IsEnabled && spin.HasAnimatedProperties, "refresh turns its icon while it reads");
        PumpUntil(() => refresh.IsEnabled, TimeSpan.FromSeconds(20));
        Check(summaries.Count > before && !spin.HasAnimatedProperties, "then it has read and stands still");
    }

    private static void CheckPages(MainWindow window, IntPtr handle, int pageCount, string pictures)
    {
        var title = Find<TextBlock>(Columns(window)[0], "PageTitle");
        var icons = Find<StackPanel>(window, "PageIcons");
        var first = title.Text;
        var height = window.ActualHeight;
        Check(icons.Children.Count == pageCount, $"one monochrome icon per provider ({icons.Children.Count})");
        Render(window, System.IO.Path.Combine(pictures, "page-1.png"));

        Find<Button>(window, "NextPageButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        var second = title.Text;
        Check(second != first, $"the next arrow turns the page ({first} -> {second})");
        Check(Settings().Contains("\"Page\":\"codex\""), "the page is kept for next time, by provider");

        // Asked for by hand on the page now shown: the limits come back from the provider itself.
        _ = window.ForceRefreshAsync();
        Pump(TimeSpan.FromMilliseconds(100));
        PumpUntil(() => Find<Button>(window, "RefreshButton").IsEnabled, TimeSpan.FromSeconds(20));
        Console.WriteLine($"     page 2: {Find<TextBlock>(Columns(window)[0], "ShortPercent").Text} / {Find<TextBlock>(Columns(window)[0], "LongPercent").Text} | {Find<TextBlock>(Columns(window)[0], "Note").Text} | {Find<TextBlock>(Columns(window)[0], "ChatText").Text}");
        Check(Math.Abs(window.ActualHeight - height) < 1, "both pages keep the card's size");
        Render(window, System.IO.Path.Combine(pictures, "page-2.png"));

        window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window)!, Environment.TickCount, System.Windows.Input.Key.Left)
        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
        Pump(TimeSpan.FromSeconds(1));
        Check(title.Text == first, "the left arrow key turns it back");

        // One swipe: several sideways steps in a row, which must turn a single page.
        for (var step = 0; step < 4; step++) SendMessage(handle, WmMouseHorizontalWheel, (IntPtr)(120 << 16), 0);
        Pump(TimeSpan.FromSeconds(1));
        Check(title.Text == second, "a sideways touchpad swipe turns it, once per swipe");

        ((Button)icons.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromSeconds(1));
        Check(title.Text == first, "a provider icon turns straight to its page");
    }

    private static void CheckApps()
    {
        Check(ClaudeApp.IsOpen(), "the Claude app is seen open (it runs these checks)");
        var codex = CodexCli.AppServer();
        Console.WriteLine($"     codex: {codex?.FileName ?? "none"} | Codex app open: {CodexApp.IsOpen()}");
    }

    private static void CheckDragKeepsProportions(MainWindow window, IntPtr handle, string when)
    {
        var rect = WindowRect(handle);
        var dragged = new PixelRect(rect.Left, rect.Top, rect.Left + rect.Width * 8 / 5, rect.Bottom);
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<PixelRect>());
        try
        {
            Marshal.StructureToPtr(dragged, buffer, false);
            SendMessage(handle, WmSizing, AspectSizing.RightEdge, buffer);
            var fitted = Marshal.PtrToStructure<PixelRect>(buffer);
            var expected = (int)Math.Round(fitted.Width * (double)rect.Height / rect.Width);
            Check(Math.Abs(fitted.Height - expected) <= 2, $"with {when}, dragging the right edge grows the height too ({rect.Width}x{rect.Height} -> {fitted.Width}x{fitted.Height})");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string Settings() => File.ReadAllText(System.IO.Path.Combine(Data, "window.json"));

    private static string SharedSettings() => File.ReadAllText(System.IO.Path.Combine(Data, "settings.json"));

    private static T Find<T>(FrameworkElement window, string name) => (T)window.FindName(name);

    private static string Line(string text) => text.Replace("\n", " | ");

    private static void Check(bool isOk, string what)
    {
        Console.WriteLine($"{(isOk ? "ok  " : "FAIL")} {what}");
        if (!isOk) _failures++;
    }

    private static void Pump(TimeSpan time) => PumpUntil(() => false, time);

    private static void PumpUntil(Func<bool> isDone, TimeSpan atMost)
    {
        var started = Stopwatch.StartNew();
        while (!isDone() && started.Elapsed < atMost)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }

    private static void Render(Window window, string file)
    {
        RenderElement((FrameworkElement)window.Content, file);
    }

    private static void RenderElement(FrameworkElement content, string file)
    {
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var context = backdrop.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }

        bitmap.Render(backdrop);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private static IntPtr SendMessage(IntPtr window, int message, int wParam, IntPtr lParam) => SendMessage(window, message, (IntPtr)wParam, lParam);

    private static IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, int lParam) => SendMessage(window, message, wParam, (IntPtr)lParam);

    private static unsafe PixelRect WindowRect(IntPtr window)
    {
        PixelRect rect;
        GetWindowRect(window, &rect);
        return rect;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetWindowRect(IntPtr window, PixelRect* rect);
}

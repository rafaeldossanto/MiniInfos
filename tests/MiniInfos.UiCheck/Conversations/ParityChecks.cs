using MiniInfos.Community;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.UiCheck;

internal static partial class Program
{
    private static void CheckConversationParity(CardSet cards, Preferences preferences, string pictures)
    {
        var usage = cards.Main;
        var window = cards.Conversations;
        window.BringBack();
        Pump(TimeSpan.FromMilliseconds(200));
        var header = Find<CardHeader>(window, "Header");
        var originalTheme = preferences.Theme;
        var originalFonts = (preferences.TextFont, preferences.NumberFont);
        try
        {
            foreach (var theme in new[] { ThemeChoice.Simple, ThemeChoice.Light, ThemeChoice.Classic, ThemeChoice.ClassicDark, ThemeChoice.ClassicLight })
            {
                preferences.Theme = theme;
                Pump(TimeSpan.FromMilliseconds(250));
                var original = Find<CardHeader>(usage, "Header");
                var artwork = Find<Image>(header, "AthenaArtwork");
                var usageArtwork = Find<Image>(original, "AthenaArtwork");
                Check(header.Height == original.Height && artwork.Visibility == usageArtwork.Visibility
                    && Equals(artwork.Source, usageArtwork.Source)
                    && Find<Grid>(header, "HeaderArt").Margin == Find<Grid>(original, "HeaderArt").Margin,
                    $"Conversations uses the same artwork, header dimensions and layout as Usage in {theme}");
                Check(Find<Border>(window, "Card").Background == Find<Border>(usage, "Card").Background,
                    $"Conversations and Usage share the {theme} palette");
                Render(window, System.IO.Path.Combine(pictures, $"conversations-{theme}.png"));
            }

            using (var image = Application.GetResourceStream(new Uri("pack://application:,,,/MiniInfos;component/Assets/athena-header.png")).Stream)
            using (var bytes = new MemoryStream())
            {
                image.CopyTo(bytes);
                var archive = System.IO.Path.Combine(pictures, "conversation-header.miniinfos");
                PackageLibrary.Create(archive, new PackageManifest(1, PackageKind.Theme, "test.conversations-header", "Header", "Test", "1.0.0", "CC0-1.0",
                    Theme: new(HeaderImage: "header.png", HeaderHeight: 114, HeaderTitleSize: 18)),
                    new Dictionary<string, byte[]> { ["header.png"] = bytes.ToArray() });
                var package = PackageLibrary.Shared.Install(archive, PackageKind.Theme);
                preferences.CommunityThemeId = package.Id;
                Pump(TimeSpan.FromMilliseconds(300));
                Check(header.Height == 114 && Find<Image>(header, "AthenaArtwork").Visibility == Visibility.Visible
                    && Find<Image>(header, "AthenaArtwork").Source == Find<Image>(Find<CardHeader>(usage, "Header"), "AthenaArtwork").Source,
                    "a community theme's local header image and dimensions apply to Conversations too");
                preferences.CommunityThemeId = null;
                PackageLibrary.Shared.Remove(PackageKind.Theme, package.Id);
            }

            preferences.TextFont = FontChoice.Cinzel;
            preferences.NumberFont = FontChoice.Orbitron;
            Pump(TimeSpan.FromMilliseconds(200));
            var rows = Find<ItemsControl>(window, "List").Items.OfType<ConversationView>().ToArray();
            var context = rows.Select(row => Find<TextBlock>(row, "Context")).First(text => text.Text.Any(char.IsDigit));
            var summary = Find<TextBlock>(window, "Summary");
            Check(new[] { context, summary }.All(text => text.Inlines.OfType<Run>().Any(run => run.Text.Any(char.IsDigit)
                    && run.FontFamily == AppFonts.Family(FontChoice.Orbitron))
                && text.Inlines.OfType<Run>().Any(run => run.Text.Any(char.IsLetter) && run.FontFamily == AppFonts.Family(FontChoice.Cinzel))),
                "context percentages and summary counts use NumberFont while labels keep CardFont");
            window.ShowConversations();
            Check(Find<ItemsControl>(window, "List").Items.OfType<ConversationView>().SequenceEqual(rows),
                "refreshes retain each conversation's row, including its rolling counter");
            Render(window, System.IO.Path.Combine(pictures, "conversations-fonts.png"));

            var face = Find<Border>(window, "Card");
            Check(window.ResizeMode == ResizeMode.CanResize && face.ContextMenu is CardContextMenu,
                "Conversations offers resizing and the same themed context menu as Usage");
            var handle = new WindowInteropHelper(window).Handle;
            CheckEdges(handle);
            CheckDragKeepsProportions(window, handle, "conversations");
            var menu = face.ContextMenu!;
            menu.PlacementTarget = face;
            menu.IsOpen = true;
            Pump(TimeSpan.FromMilliseconds(150));
            RenderElement(menu, System.IO.Path.Combine(pictures, "conversations-menu.png"));
            menu.IsOpen = false;
            var topmost = Find<MenuItem>(window, "TopmostItem");
            topmost.IsChecked = false;
            Check(!preferences.IsTopmost && !window.Topmost && !usage.Topmost, "the context-menu topmost toggle follows the shared preference");
            topmost.IsChecked = true;

            var saved = new JsonFile<ConversationsSettings>(System.IO.Path.Combine(Data, "conversations.json"));
            window.Width = 444;
            SendMessage(handle, 0x0232, 0, IntPtr.Zero);
            Check(Math.Abs(saved.Load()!.Scale - 1.5) < 0.01, "Conversations saves its own resize scale");
            var restored = new ConversationsWindow(() => cards.Providers, preferences);
            restored.ShowWithAssistant(null);
            Pump(TimeSpan.FromMilliseconds(200));
            Check(Math.Abs(restored.Width - 444) < 1, "the conversation card restores its saved scale on the next launch");
            restored.PrepareToQuit();
            restored.Close();

            usage.IsDetailed = false;
            window.IsDetailed = true;
            usage.HideCard();
            Find<Button>(window, "SettingsButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(200));
            var settings = cards.Settings!;
            Check(settings.Card == window && settings.Owner == window && !usage.IsVisible
                && Find<RadioButton>(settings, "DetailedNumbers").IsChecked == true,
                "Conversations opens settings for itself without bringing hidden Usage back");
            Find<RadioButton>(settings, "CompactNumbers").IsChecked = true;
            Check(!window.IsDetailed && !usage.IsDetailed, "settings changes Conversations' number mode independently");
            window.IsDetailed = true;
            Check(Find<RadioButton>(settings, "DetailedNumbers").IsChecked == true && !usage.IsDetailed,
                "the conversation card's view switch updates its settings, leaving Usage alone");
            var usagePosition = (usage.Left, usage.Top, usage.Width, usage.Height);
            Find<Button>(settings, "DefaultSizeButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(Math.Abs(window.Width - 296) < 1 && (usage.Left, usage.Top, usage.Width, usage.Height) == usagePosition,
                "Tamanho padrão resets Conversations alone");
            window.Left = 40;
            window.Top = 40;
            Find<Button>(settings, "CornerButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(Math.Abs(window.Left + window.Width + 16 - SystemParameters.WorkArea.Right) < 1
                && Math.Abs(window.Top + window.Height + 16 - SystemParameters.WorkArea.Bottom) < 1
                && (usage.Left, usage.Top, usage.Width, usage.Height) == usagePosition,
                "Pro canto moves Conversations alone");
            settings.Close();
            cards.OpenSettings(usage);
            Check(cards.Settings!.Card == usage && usage.IsVisible, "Usage still opens settings for its own card");
            cards.Settings.Close();
        }
        finally
        {
            preferences.CommunityThemeId = null;
            preferences.Theme = originalTheme;
            preferences.TextFont = originalFonts.TextFont;
            preferences.NumberFont = originalFonts.NumberFont;
        }
    }
}

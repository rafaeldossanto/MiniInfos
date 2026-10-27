namespace MiniInfos.UiCheck;

internal static partial class Program
{
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
        Check(banner.IsVisible && message == MiniInfos.Localization.Localizer.Text("Nova atualização disponível") && button.IsEnabled,
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
        Check(Shown(Find<TextBlock>(banner, "Message")).StartsWith(MiniInfos.Localization.Localizer.Text("Instalando")) && !Find<Button>(banner, "DownloadButton").IsEnabled,
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
}

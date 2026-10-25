using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        var providers = UsageProviders.Create();
        var cards = new CardSet(providers);
        var window = cards.Main;
        var summaries = new List<string>();
        window.SummaryChanged += summaries.Add;
        using var tray = new TrayIcon(cards);

        var opening = Stopwatch.StartNew();
        window.Show();
        PumpUntil(() => window.Opacity == 1, TimeSpan.FromSeconds(10));
        Check(window.Opacity == 1 && opening.Elapsed < TimeSpan.FromSeconds(4), $"the card shows quickly ({opening.Elapsed.TotalSeconds:0.0} s)");
        Pump(TimeSpan.FromSeconds(1));
        var handle = new WindowInteropHelper(window).Handle;

        CheckEdges(handle);
        CheckTray(tray, summaries, window);
        CheckViews(window);
        CheckRefresh(window, summaries);
        CheckPages(window, handle, providers.Count, pictures);
        CheckSplit(window, handle, pictures);
        CheckNewWindow(cards, pictures);
        CheckApps();
        CheckDragKeepsProportions(window, handle, "one page");

        cards.Quit();
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
        Check(Find<TextBlock>(window, "UpdatedText").Text == "" && Find<TextBlock>(Columns(window)[1], "Note").Text != "",
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
        Console.WriteLine($"     page 2: {Find<TextBlock>(Columns(window)[0], "ShortPercent").Text} / {Find<TextBlock>(Columns(window)[0], "LongPercent").Text} | {Find<TextBlock>(window, "UpdatedText").Text} | {Find<TextBlock>(Columns(window)[0], "ChatText").Text}");
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

    private static string Settings() =>
        File.ReadAllText(System.IO.Path.Combine(Environment.GetEnvironmentVariable("MINIINFOS_DATA_DIR")!, "window.json"));

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
        var content = (FrameworkElement)window.Content;
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

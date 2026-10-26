namespace MiniInfos.UiCheck;

internal static partial class Program
{
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

    private static string Settings() => File.ReadAllText(System.IO.Path.Combine(Data, "window.json"));

    private static string SharedSettings() => File.ReadAllText(System.IO.Path.Combine(Data, "settings.json"));

    private static T Find<T>(FrameworkElement window, string name) => (T)window.FindName(name);

    private static string Line(string text) => text.Replace("\n", " | ");

    private static void Check(bool isOk, string what)
    {
        _checks++;
        if (!isOk || !_quiet) Console.WriteLine($"{(isOk ? "ok  " : "FAIL")} {what}");
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

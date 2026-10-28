using Avalonia;

namespace MiniInfos.Desktop;

internal static class Program
{
    internal static Platform.SingleInstance? Instance { get; private set; }
    [STAThread]
    public static int Main(string[] args)
    {
        // Headless read used by native CI: runs actual discovery/ledgers without opening a window.
        using var instance = new Platform.SingleInstance();
        if (!instance.IsPrimary)
        {
            if (args.Contains("--diagnostics")) { Console.Error.WriteLine("Close MiniInfos or use an isolated MINIINFOS_DATA_DIR for diagnostics."); return 1; }
            try { instance.ActivateAsync().GetAwaiter().GetResult(); return 0; }
            catch (IOException) { return 1; }
            catch (TimeoutException) { return 1; }
        }
        if (args.Contains("--diagnostics")) return Diagnostics.RunAsync().GetAwaiter().GetResult();
        Instance = instance;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DesktopApp>().UsePlatformDetect().LogToTrace();
}

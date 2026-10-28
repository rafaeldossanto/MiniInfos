using System.Security;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Desktop.Platform;

internal static class StartupIntegration
{
    private static string FilePath => OperatingSystem.IsMacOS()
        ? Path.Combine(HostPaths.Home, "Library", "LaunchAgents", "com.miniinfos.desktop.plist")
        : Path.Combine(HostPaths.Configuration, "autostart", "miniinfos.desktop");
    public static bool IsEnabled => File.Exists(FilePath);
    public static void SetEnabled(bool enabled)
    {
        if (!enabled) { File.Delete(FilePath); return; }
        var executable = Environment.ProcessPath ?? throw new IOException("The executable path is unavailable.");
        if (!Path.IsPathRooted(executable)) throw new IOException("The executable path must be absolute.");
        var contents = OperatingSystem.IsMacOS()
            ? "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n<plist version=\"1.0\"><dict><key>Label</key><string>com.miniinfos.desktop</string><key>ProgramArguments</key><array><string>" + SecurityElement.Escape(executable) + "</string></array><key>RunAtLoad</key><true/></dict></plist>\n"
            : "[Desktop Entry]\nType=Application\nName=MiniInfos\nExec=\"" + executable.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%") + "\"\nTerminal=false\nX-GNOME-Autostart-enabled=true\n";
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, contents);
    }
}

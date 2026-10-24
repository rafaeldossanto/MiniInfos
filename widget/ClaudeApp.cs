using System.ComponentModel;
using System.Diagnostics;

namespace MiniInfos;

/// <summary>
/// Whether the Claude desktop app has its window open. Its process and the Claude Code
/// chats it starts share the name claude.exe; only the app has a window, and the chats
/// run from a claude-code folder.
/// </summary>
public static class ClaudeApp
{
    private const string ProcessName = "claude";
    private const string CodeFolder = @"\claude-code\";

    public static bool IsOpen()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        try
        {
            return processes.Any(IsAppWindow);
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static bool IsAppWindow(Process process)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero && !RunsFromCodeFolder(process);
        }
        catch (InvalidOperationException)
        {
            // Gone meanwhile.
            return false;
        }
    }

    private static bool RunsFromCodeFolder(Process process)
    {
        try
        {
            return process.MainModule?.FileName.Contains(CodeFolder, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Win32Exception)
        {
            // The Store app may not let other programs read where it runs from: its window is enough.
            return false;
        }
    }
}

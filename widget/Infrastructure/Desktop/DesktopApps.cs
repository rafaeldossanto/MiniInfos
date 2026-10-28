using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiniInfos.Infrastructure.Windows;

/// <summary>
/// Whether a desktop app is open: a process of its name with a main window, running from a path
/// the caller accepts. The path is read with the least access Windows grants, which Store apps allow.
/// </summary>
public static partial class DesktopApps
{
    private const uint QueryLimitedInformation = 0x1000;

    /// <param name="acceptsPath">Judges where the process runs from; handed null when Windows keeps the path from us.</param>
    public static bool HasWindow(string processName, Func<string?, bool> acceptsPath)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Any(process => IsWindowOf(process, acceptsPath));
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static bool IsWindowOf(Process process, Func<string?, bool> acceptsPath)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero && acceptsPath(PathOf(process.Id));
        }
        catch (InvalidOperationException)
        {
            // Gone meanwhile.
            return false;
        }
    }

    /// <summary>Whether any process of this name runs, with a window or without.</summary>
    public static bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var process in processes) process.Dispose();
        return processes.Length > 0;
    }

    public static IReadOnlyList<string> ExecutablePaths(params string[] names) => ExecutablePaths(false, names);

    public static IReadOnlyList<string> ExecutablePaths(bool withoutWindow, params string[] names)
    {
        var paths = new List<string>();
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var process in Process.GetProcessesByName(name))
            using (process)
            {
                try { if ((!withoutWindow || process.MainWindowHandle == 0) && PathOf(process.Id) is { } path) paths.Add(path); }
                catch (InvalidOperationException) { }
            }
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? PathOf(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, inheritHandle: false, processId);
        if (handle == IntPtr.Zero) return null;

        try
        {
            var name = new char[1024];
            var length = (uint)name.Length;
            return QueryFullProcessImageName(handle, 0, name, ref length) ? new string(name, 0, (int)length) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(IntPtr process, uint flags, [Out] char[] name, ref uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}

using System.IO;
using System.Runtime.InteropServices;

namespace MiniInfos;

/// <summary>
/// The shortcut in the Windows startup folder that opens MiniInfos at sign-in, hidden until Claude or
/// Codex opens: the one the installer's "Abrir junto com Claude ou Codex" option makes, and the
/// settings window turns on and off.
/// </summary>
public sealed class StartupShortcut(string folder)
{
    /// <summary>What the shortcut passes: start hidden and wait for an app it follows.</summary>
    public const string Arguments = "--with-claude";

    /// <summary>The installer's shortcut, in the user's startup folder.</summary>
    public static StartupShortcut Default { get; } = new(AppPaths.Startup);

    private string FilePath => Path.Combine(folder, "MiniInfos.lnk");

    public bool IsOn
    {
        get => File.Exists(FilePath);
        set
        {
            if (value == IsOn) return;

            if (value) Create();
            else Delete();
        }
    }

    private void Create()
    {
        if (Environment.ProcessPath is not { } app || Type.GetTypeFromProgID("WScript.Shell") is not { } shellType) return;

        object? shell = null;
        object? link = null;
        try
        {
            Directory.CreateDirectory(folder);
            shell = Activator.CreateInstance(shellType);
            link = ((dynamic)shell!).CreateShortcut(FilePath);
            dynamic shortcut = link!;
            shortcut.TargetPath = app;
            shortcut.Arguments = Arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(app);
            shortcut.Description = "Limites e tokens do Claude e do Codex";
            shortcut.Save();
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            if (link is not null) Marshal.FinalReleaseComObject(link);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }

    private void Delete()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

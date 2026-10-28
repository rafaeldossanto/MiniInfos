using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace MiniInfos.Infrastructure.Desktop;

/// <summary>Starts a command found with <see cref="CommandPath"/> or anywhere else, as a terminal would.</summary>
public static class CommandRunner
{
    /// <summary>How to start it: a batch file, like npm's .cmd, runs through cmd.exe, since a process can't start one by itself.</summary>
    public static ProcessStartInfo StartInfo(string command, string arguments) =>
        Path.GetExtension(command).Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", $"/d /s /c \"\"{command}\" {arguments}\"")
            : new ProcessStartInfo(command, arguments);

    /// <summary>Runs it out of sight to the end.</summary>
    /// <returns>Its exit code, or -1 when it could not start or did not finish within <paramref name="timeout"/>.</returns>
    public static int Run(string command, string arguments, TimeSpan timeout)
    {
        var start = StartInfo(command, arguments);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;

        try
        {
            using var process = Process.Start(start);
            if (process is null) return -1;

            // Both streams read as they come, so a chatty command never blocks on a full pipe.
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (process.WaitForExit(timeout)) return process.ExitCode;

            process.Kill(entireProcessTree: true);
            return -1;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return -1;
        }
    }
}

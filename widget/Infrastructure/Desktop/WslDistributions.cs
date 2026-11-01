using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MiniInfos.Infrastructure.Desktop;

/// <summary>Queries running distributions only. No command starts a Linux process or opens a stopped distribution.</summary>
internal static class WslDistributions
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static IReadOnlyList<string> Running()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
        if (!File.Exists(executable)) return [];
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.Unicode,
        };
        foreach (var argument in new[] { "--list", "--running", "--quiet" }) start.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(start);
            if (process is null) return [];
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                return [];
            }
            // A closed stdout is part of completion; never wait indefinitely for a pipe.
            if (process.ExitCode != 0 || !Task.WaitAll([output, error], Timeout)) return [];
            return Parse(output.Result);
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException or AggregateException)
        {
            return [];
        }
    }

    internal static IReadOnlyList<string> Parse(string output) => output.Replace("\0", "").Trim('\uFEFF')
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(ValidName).Where(name => !name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(32).ToArray();

    internal static bool ValidName(string name) => name.Length is > 0 and <= 128 && name is not "." and not ".."
        && name.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ' ');

    public static bool IsPath(string path) => path.StartsWith(@"\\wsl.localhost\", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(@"\\wsl$\", StringComparison.OrdinalIgnoreCase);
}

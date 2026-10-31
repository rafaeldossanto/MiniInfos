using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MiniInfos.Infrastructure.Desktop;

/// <summary>Local listeners belonging to the installed agent. Authentication stays in memory and is never logged.</summary>
internal sealed class AntigravityServerEndpoint(int port, string csrf)
{
    public int Port { get; } = port;
    public string Csrf { get; } = csrf;
}

internal static partial class AntigravityServerDiscovery
{
    public static async Task<IReadOnlyList<AntigravityServerEndpoint>> FindAsync(CancellationToken cancel)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return await Windows(cancel);
            if (OperatingSystem.IsLinux()) return Linux();
            if (OperatingSystem.IsMacOS()) return await Mac(cancel);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or Win32Exception or InvalidOperationException)
        { /* No agent, no permission, or it exited during discovery. */ }
        return [];
    }

    private static async Task<IReadOnlyList<AntigravityServerEndpoint>> Windows(CancellationToken cancel)
    {
        const string script = """
            $ErrorActionPreference='Stop'
            $results=@()
            Get-CimInstance Win32_Process -Filter "Name LIKE 'language_server%.exe'" | ForEach-Object {
              if ($_.ExecutablePath -notmatch '(?i)[\\/]antigravity[\\/].*[\\/]language_server[^\\/]*\.exe$') { return }
              $csrf=[regex]::Match($_.CommandLine,'--csrf_token(?:=|\s+)(?:"([^"]+)"|(\S+))')
              if (!$csrf.Success) { return }
              $token=if($csrf.Groups[1].Success){$csrf.Groups[1].Value}else{$csrf.Groups[2].Value}
              Get-NetTCPConnection -State Listen -OwningProcess $_.ProcessId -ErrorAction SilentlyContinue | ForEach-Object {
                if ($_.LocalAddress -eq '127.0.0.1') { $results+=@{Port=$_.LocalPort;Csrf=$token} }
              }
            }
            ConvertTo-Json -InputObject @($results) -Compress
            """;
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var output = await Capture(shell, ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))], cancel);
        if (string.IsNullOrWhiteSpace(output)) return [];
        using var json = JsonDocument.Parse(output);
        return json.RootElement.EnumerateArray().Select(item => new AntigravityServerEndpoint(item.GetProperty("Port").GetInt32(), item.GetProperty("Csrf").GetString() ?? ""))
            .Where(Valid).OrderBy(item => item.Port).ToArray();
    }

    private static IReadOnlyList<AntigravityServerEndpoint> Linux()
    {
        var found = new List<AntigravityServerEndpoint>();
        foreach (var process in Process.GetProcessesByName("language_server").Concat(Process.GetProcessesByName("language_server_linux_x64")).Concat(Process.GetProcessesByName("language_server_linux_arm")))
        using (process)
        {
            try
            {
                var root = "/proc/" + process.Id;
                var executable = new FileInfo(root + "/exe").ResolveLinkTarget(true)?.FullName ?? "";
                if (!TrustedExecutable(executable)) continue;
                var token = Token(File.ReadAllText(root + "/cmdline").Replace('\0', ' '));
                if (token is null) continue;
                var sockets = Directory.EnumerateFiles(root + "/fd").Select(path => new FileInfo(path).LinkTarget)
                    .OfType<string>().Where(value => value.StartsWith("socket:[")).ToHashSet();
                foreach (var line in File.ReadLines("/proc/net/tcp").Skip(1))
                {
                    var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length < 10 || columns[3] != "0A" || !columns[1].StartsWith("0100007F:") || !sockets.Contains("socket:[" + columns[9] + "]")) continue;
                    found.Add(new AntigravityServerEndpoint(Convert.ToInt32(columns[1].Split(':')[1], 16), token));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { }
        }
        return found.Where(Valid).OrderBy(item => item.Port).ToArray();
    }

    private static async Task<IReadOnlyList<AntigravityServerEndpoint>> Mac(CancellationToken cancel)
    {
        var found = new List<AntigravityServerEndpoint>();
        var listing = await Capture("/bin/ps", ["-axo", "pid=,args="], cancel);
        foreach (var line in listing.Split('\n'))
        {
            var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var pid) || !TrustedExecutable(parts[1]) || Token(parts[1]) is not { } token) continue;
            var listeners = await Capture("/usr/sbin/lsof", ["-nP", "-a", "-p", pid.ToString(), "-iTCP", "-sTCP:LISTEN", "-Fn"], cancel);
            foreach (var listener in listeners.Split('\n'))
                if (listener.StartsWith("n127.0.0.1:") && int.TryParse(listener[11..], out var port)) found.Add(new AntigravityServerEndpoint(port, token));
        }
        return found.Where(Valid).OrderBy(item => item.Port).ToArray();
    }

    internal static bool TrustedExecutable(string path) => path.Contains("antigravity/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("antigravity.app/", StringComparison.OrdinalIgnoreCase);
    internal static string? Token(string arguments)
    {
        var match = CsrfArgument().Match(arguments);
        var token = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return match.Success && token.Length is > 0 and <= 512 && !token.Any(char.IsControl) ? token : null;
    }
    private static bool Valid(AntigravityServerEndpoint endpoint) => endpoint.Port is > 0 and <= 65535 && endpoint.Csrf.Length is > 0 and <= 512 && !endpoint.Csrf.Any(char.IsControl);
    [GeneratedRegex("--csrf_token(?:=|\\s+)(?:\"([^\"]+)\"|(\\S+))")]
    private static partial Regex CsrfArgument();

    private static async Task<string> Capture(string executable, string[] arguments, CancellationToken cancel)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        if (process is null) return "";
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancel);
            var error = process.StandardError.ReadToEndAsync(cancel);
            await process.WaitForExitAsync(cancel);
            await error;
            return process.ExitCode == 0 ? await output : "";
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
}

using System.IO;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Sources;

/// <summary>Refreshes the source catalogue off the UI thread, keeping the last useful paths between searches.</summary>
public sealed class UsageSources
{
    public static UsageSources Shared { get; } = new();
    /// <summary>Install folders rarely move, and a search walks up to thousands of folders: once in a while is enough (â†» searches at once).</summary>
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(10);
    private readonly Lock _gate = new();
    private readonly JsonFile<UsageSourceSnapshot> _cache = new(Path.Combine(AppPaths.Data, "usage-sources.json"));
    private UsageSourceSnapshot _current;
    private DateTimeOffset _searchedAt = DateTimeOffset.MinValue;
    private Task? _search;

    private UsageSources()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var saved = _cache.Load();
        _current = new(
            SourceFiles.Distinct(new[] { Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"), Path.Combine(profile, ".claude") }.Concat(saved?.ClaudeRoots ?? [])),
            SourceFiles.Distinct(new[] { Environment.GetEnvironmentVariable("CODEX_HOME"), Path.Combine(profile, ".codex") }.Concat(saved?.CodexRoots ?? [])));
    }

    public UsageSourceSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                if (_search is null && DateTimeOffset.UtcNow - _searchedAt >= RefreshEvery) Start();
                return _current;
            }
        }
    }

    public Task RefreshAsync()
    {
        lock (_gate) return _search ?? Start();
    }

    private Task Start()
    {
        _searchedAt = DateTimeOffset.UtcNow;
        return _search = Task.Run(() =>
        {
            try
            {
                var context = CaptureContext();
                UsageSourceSnapshot remembered;
                lock (_gate) remembered = _current;
                var found = UsageSourceDiscovery.Find(context, remembered);
                lock (_gate) _current = found;
                _cache.Save(found);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            {
                // Keep the last catalogue if a drive or app disappears during discovery.
            }
            finally { lock (_gate) _search = null; }
        });
    }

    private static SourceSearchContext CaptureContext()
    {
        var roots = new List<string>();
        foreach (var drive in OperatingSystem.IsWindows() ? DriveInfo.GetDrives() : [])
        {
            try { if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable) roots.Add(drive.RootDirectory.FullName); }
            catch (IOException) { }
        }
        var apps = DesktopApps.ExecutablePaths("codex", "claude", "Codex", "ChatGPT")
            .Where(path => !Path.GetFileName(path).Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase)
                || path.Contains("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => Ancestors(Path.GetDirectoryName(path))).ToArray();
        return new(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            HostPaths.Data,
            HostPaths.Configuration,
            EnvironmentHomes("CLAUDE_CONFIG_DIR"), EnvironmentHomes("CODEX_HOME"), roots, apps);
    }

    public static IReadOnlyList<string> EnvironmentHomes(string variable) => SourceFiles.Distinct(
        (OperatingSystem.IsWindows() ? new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine } : [EnvironmentVariableTarget.Process])
            .Select(target => Environment.GetEnvironmentVariable(variable, target)));

    private static IEnumerable<string> Ancestors(string? path)
    {
        for (var level = 0; path is not null && level < 3; level++, path = Path.GetDirectoryName(path)) yield return path;
    }
}

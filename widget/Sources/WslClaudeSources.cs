using System.IO;
using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Sources;

/// <summary>Default Claude histories in running WSL homes. These paths never enter the persisted Windows catalogue.</summary>
internal static class WslClaudeSources
{
    public static IReadOnlyList<string> Projects(Func<IReadOnlyList<string>>? running = null, Func<string, string>? root = null)
    {
        if (running is null && !OperatingSystem.IsWindows()) return [];
        running ??= WslDistributions.Running;
        root ??= name => @"\\wsl.localhost\" + name;
        var projects = new List<string>();
        foreach (var name in running().Where(WslDistributions.ValidName).Distinct(StringComparer.OrdinalIgnoreCase).Take(32))
        {
            var distribution = root(name);
            var homes = SourceFiles.Directories(Path.Combine(distribution, "home")).Take(64)
                .Append(Path.Combine(distribution, "root"));
            foreach (var home in homes)
            {
                var folder = Path.Combine(home, ".claude", "projects");
                if (Directory.Exists(folder)) projects.Add(folder);
            }
        }
        // Linux homes are case-sensitive even though the reader itself runs on Windows.
        return projects.Distinct(StringComparer.Ordinal).ToArray();
    }
}

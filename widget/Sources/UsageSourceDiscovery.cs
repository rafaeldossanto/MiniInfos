using System.Diagnostics;
using System.IO;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Sources;

public static class UsageSourceDiscovery
{
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "Users", "Documents and Settings",
        "$RECYCLE.BIN", "System Volume Information", ".git", "node_modules", ".venv", "venv", ".cache",
        "Temp", "Cache", "Code Cache", "GPUCache", "worktrees", "target", "bin", "obj", "AppData",
    };

    /// <summary>What only Claude Code keeps in its home: a projects folder beside a settings.json alone is any project.</summary>
    private static readonly string[] ClaudeMarkers = [".credentials.json", ".claude.json", "history.jsonl", "stats-cache.json"];

    /// <summary>What only Codex keeps in its home: a config.toml alone is any tool's.</summary>
    private static readonly string[] CodexMarkers = ["installation_id", ".codex-global-state.json"];

    public static UsageSourceSnapshot Find(SourceSearchContext context, UsageSourceSnapshot? remembered = null)
    {
        var claude = new List<string>(context.ClaudeHomes);
        var codex = new List<string>(context.CodexHomes);
        claude.Add(Path.Combine(context.UserProfile, ".claude"));
        codex.Add(Path.Combine(context.UserProfile, ".codex"));
        claude.AddRange((remembered?.ClaudeRoots ?? []).Where(Directory.Exists));
        codex.AddRange((remembered?.CodexRoots ?? []).Where(Directory.Exists));

        var packages = SourceFiles.Directories(Path.Combine(context.LocalAppData, "Packages"))
            .Where(path => IsClaudeName(Path.GetFileName(path)) || Path.GetFileName(path).StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)).ToArray();
        var direct = new[]
        {
            Path.Combine(context.UserProfile, ".config"),
            Path.Combine(context.LocalAppData, "Claude"), Path.Combine(context.LocalAppData, "Anthropic"),
            Path.Combine(context.RoamingAppData, "Claude"),
            Path.Combine(context.LocalAppData, "OpenAI", "Codex"), Path.Combine(context.RoamingAppData, "OpenAI", "Codex"),
            Path.Combine(context.LocalAppData, "Codex"),
        }.Concat(packages).Concat(context.AppFolders).ToArray();

        foreach (var folder in new[] { Path.Combine(context.LocalAppData, "MiniInfos", "sources") }
            .Concat(packages.Select(package => Path.Combine(package, "LocalCache", "Local", "MiniInfos", "sources"))))
        {
            foreach (var file in SourceFiles.Files(folder, "*.json"))
            {
                var source = new JsonFile<SourceRegistration>(file).Load();
                if (source?.Provider == "claude" && SourceFiles.Normalize(source.Root) is { } root && Directory.Exists(root)) claude.Add(root);
            }
        }

        var queue = new Queue<(string Path, int Depth)>();
        foreach (var root in SourceFiles.Distinct(direct)) queue.Enqueue((root, 5));
        queue.Enqueue((context.UserProfile, 4));
        foreach (var root in SourceFiles.Distinct(context.SearchRoots)) queue.Enqueue((root, 3));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clock = Stopwatch.StartNew();
        while (queue.Count > 0 && visited.Count < 10_000 && clock.Elapsed < TimeSpan.FromSeconds(4))
        {
            var (path, depth) = queue.Dequeue();
            if (!visited.Add(path) || !Directory.Exists(path)) continue;
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            var isClaude = Directory.Exists(Path.Combine(path, "projects"))
                && (IsClaudeName(name) || HasAny(path, ClaudeMarkers));
            isClaude |= Directory.Exists(Path.Combine(path, "miniinfos", "sessions"));
            var isCodex = (Directory.Exists(Path.Combine(path, "sessions")) || Directory.Exists(Path.Combine(path, "archived_sessions")))
                && (name.Contains("codex", StringComparison.OrdinalIgnoreCase) || HasAny(path, CodexMarkers)
                    || (File.Exists(Path.Combine(path, "auth.json")) && File.Exists(Path.Combine(path, "config.toml"))));
            if (isClaude) claude.Add(path);
            if (isCodex) codex.Add(path);
            if (isClaude || isCodex || depth <= 0) continue;
            foreach (var child in SourceFiles.Directories(path))
            {
                if (Skip.Contains(Path.GetFileName(child))) continue;
                try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                queue.Enqueue((child, depth - 1));
            }
        }
        return new(SourceFiles.Distinct(claude), SourceFiles.Distinct(codex));
    }

    private static bool IsClaudeName(string name) => name.Contains("claude", StringComparison.OrdinalIgnoreCase);

    private static bool HasAny(string folder, IEnumerable<string> files) => files.Any(file => File.Exists(Path.Combine(folder, file)));

    private sealed record SourceRegistration(string Provider, string Root);
}

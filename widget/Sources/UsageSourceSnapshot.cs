using System.IO;
using System.Text.Json.Serialization;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Sources;

public sealed record UsageSourceSnapshot(IReadOnlyList<string> ClaudeRoots, IReadOnlyList<string> CodexRoots)
{
    [JsonIgnore] public IReadOnlyList<string> ClaudeProjects => SourceFiles.Distinct(ClaudeRoots.Select(root => Path.Combine(root, "projects")));
    [JsonIgnore] public IReadOnlyList<string> ClaudeFeeds => SourceFiles.Distinct(ClaudeRoots.Select(root => Path.Combine(root, "miniinfos", "sessions")));
    [JsonIgnore] public IReadOnlyList<string> CodexSessions => SourceFiles.Distinct(CodexRoots.SelectMany(root =>
        new[] { Path.Combine(root, "sessions"), Path.Combine(root, "archived_sessions") }));
}

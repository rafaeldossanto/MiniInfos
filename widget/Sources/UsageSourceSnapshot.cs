using System.IO;
using System.Text.Json.Serialization;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Sources;

public sealed record UsageSourceSnapshot(IReadOnlyList<string> ClaudeRoots, IReadOnlyList<string> CodexRoots)
{
    // Worked out once per catalogue: normalizing a path asks the disk, and the readers ask for these lists on
    // every refresh, while a catalogue only changes when the background search finds something new.
    [JsonIgnore] public IReadOnlyList<string> ClaudeProjects { get; } = SourceFiles.Distinct(ClaudeRoots.Select(root => Path.Combine(root, "projects")));
    [JsonIgnore] public IReadOnlyList<string> ClaudeFeeds { get; } = SourceFiles.Distinct(ClaudeRoots.Select(root => Path.Combine(root, "miniinfos", "sessions")));
    [JsonIgnore] public IReadOnlyList<string> CodexSessions { get; } = SourceFiles.Distinct(CodexRoots.SelectMany(root =>
        new[] { Path.Combine(root, "sessions"), Path.Combine(root, "archived_sessions") }));
}

using System.IO;
using MiniInfos.Sources;

namespace MiniInfos.Providers.Claude;

public static class ClaudePaths
{
    public static IReadOnlyList<string> ConfigRoots => UsageSources.Shared.Current.ClaudeRoots;
    public static IReadOnlyList<string> ProjectFolders => UsageSources.Shared.Current.ClaudeProjects;
    public static IReadOnlyList<string> FeedFolders => UsageSources.Shared.Current.ClaudeFeeds;

    public static string ConfigRoot => ConfigRoots[0];
    public static string Projects => Path.Combine(ConfigRoot, "projects");
    public static string Feed => Path.Combine(ConfigRoot, "miniinfos", "sessions");
}

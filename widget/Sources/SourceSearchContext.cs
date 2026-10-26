namespace MiniInfos.Sources;

/// <summary>Inputs are explicit so discovery can be checked without changing the person's environment.</summary>
public sealed record SourceSearchContext(
    string UserProfile,
    string LocalAppData,
    string RoamingAppData,
    IReadOnlyList<string> ClaudeHomes,
    IReadOnlyList<string> CodexHomes,
    IReadOnlyList<string> SearchRoots,
    IReadOnlyList<string> AppFolders);

using System.IO;
using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers.Antigravity;

public static class AntigravityPaths
{
    // Antigravity 2 stores agent histories here on Windows, Linux and macOS, separately from Gemini CLI's tmp.
    public static IReadOnlyList<string> ConversationFolders => Find(HostPaths.Home);

    internal static IReadOnlyList<string> Find(string home) => SourceFiles.Distinct([
        Path.Combine(home, ".gemini", "antigravity", "conversations"),
    ]).Where(Directory.Exists).ToArray();
}

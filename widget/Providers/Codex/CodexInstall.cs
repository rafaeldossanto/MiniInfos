using MiniInfos.Infrastructure.Desktop;

namespace MiniInfos.Providers.Codex;

/// <summary>
/// Where an install of Codex shows: the Store app, the codex command or its editor extension. The
/// .codex folder and the copy of codex the app keeps under %LOCALAPPDATA% are no sign: both stay after
/// an uninstall. The ChatGPT app's process shares a name with the Store app's, so processes count by codex alone.
/// </summary>
public static class CodexInstall
{
    public static AppFootprint Footprint { get; } = new()
    {
        StorePackages = ["OpenAI.Codex_2p2nqsd0c76g0"],
        Bundles = HostPaths.AppBundles("Codex"),
        Commands = ["codex"],
        EditorExtensions = ["openai.chatgpt"],
        Processes = ["codex"],
    };

    public static bool IsPresent() => InstalledApps.Has(Footprint, InstallContext.Current());
}

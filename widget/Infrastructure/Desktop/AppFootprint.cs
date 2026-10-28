namespace MiniInfos.Infrastructure.Desktop;

/// <summary>
/// What installing an app leaves for this Windows user and uninstalling takes away, as
/// <see cref="InstalledApps"/> looks for it. Its data folders are no part of it: they stay behind after an uninstall.
/// </summary>
public sealed record AppFootprint
{
    /// <summary>Store package family names (name_publisherId).</summary>
    public IReadOnlyList<string> StorePackages { get; init; } = [];

    /// <summary>Commands on PATH, named without their extension.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>VS Code extension ids (publisher.name), in VS Code or one of its forks.</summary>
    public IReadOnlyList<string> EditorExtensions { get; init; } = [];

    /// <summary>Files an installer lays where no PATH may name them.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>macOS application bundles; their presence is an install, unlike a leftover data folder.</summary>
    public IReadOnlyList<string> Bundles { get; init; } = [];

    /// <summary>Process names: one running means it is installed, however that was done.</summary>
    public IReadOnlyList<string> Processes { get; init; } = [];
}

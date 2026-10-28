namespace MiniInfos.Infrastructure.Desktop;

/// <summary>Where <see cref="InstalledApps"/> looks: this PC, or folders and answers a test sets up.</summary>
/// <param name="Path">A PATH value: folders separated by ';'.</param>
/// <param name="UserProfile">The folder editors keep their extensions under.</param>
public sealed record InstallContext(string Path, string UserProfile, Func<string, bool> HasStorePackage, Func<string, bool> IsRunning)
{
    /// <summary>This PC as it is now.</summary>
    public static InstallContext Current() => new(
        CommandPath.Current(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        InstalledApps.HasStorePackage,
        DesktopApps.IsRunning);
}

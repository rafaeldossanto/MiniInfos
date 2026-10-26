namespace MiniInfos.Infrastructure;

/// <summary>What MiniInfos says of itself to other programs.</summary>
public static class AppInfo
{
    /// <summary>The version the project file sets: the one the installer is built with too.</summary>
    public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

namespace MiniInfos.Updates;

/// <summary>Where the latest version is announced.</summary>
public interface IUpdateSource
{
    /// <summary>The latest release when it is newer than <paramref name="current"/>; null otherwise, and when it can't be read.</summary>
    Task<AvailableUpdate?> NewerThanAsync(Version current, CancellationToken cancellation);
}

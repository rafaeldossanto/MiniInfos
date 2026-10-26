namespace MiniInfos.Providers;

/// <summary>A source of usage numbers with a page of its own on the card: Claude, Codex, or the next one.</summary>
public interface IUsageProvider
{
    /// <summary>Saved with the card's settings so it reopens on the same page; never shown.</summary>
    string Id { get; }

    /// <summary>Whether its app is on this PC: only installed providers get a page (<see cref="InstalledProviders"/>).</summary>
    bool IsInstalled();

    /// <summary>Whether its app or one of its chats is open now: the card shows when one opens.</summary>
    bool IsInUse();

    /// <summary>Reads the latest numbers; every provider reads on each refresh so its ledger keeps up with its logs.</summary>
    /// <param name="isShown">Its page is on screen: reads only a visible page needs (starting a process, asking a server) may run.</param>
    /// <param name="force">The person asked for fresh numbers (↻): wait for those reads instead of using the last ones.</param>
    Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force);

    /// <summary>Writes what it keeps before the app quits.</summary>
    void Flush();
}

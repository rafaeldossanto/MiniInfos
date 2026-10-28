namespace MiniInfos.Providers;

/// <summary>
/// Conversation titles read from small files beside their logs, read again once a minute: a title changes
/// rarely, and the conversations card asks for every one every few seconds.
/// </summary>
public sealed class TitleCache
{
    private static readonly TimeSpan ReadEvery = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, (string? Title, DateTimeOffset ReadAt)> _titles = [];

    /// <param name="read">Reads the title anew; null when the conversation has none.</param>
    public string? Of(string key, Func<string?> read)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (_titles.TryGetValue(key, out var known) && now - known.ReadAt < ReadEvery) return known.Title;

            var title = read();
            _titles[key] = (title, now);
            return title;
        }
    }
}

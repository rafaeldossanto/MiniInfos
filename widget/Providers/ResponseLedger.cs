using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers;

/// <summary>
/// One model response read from a log line: the key that names it, its tokens, an optional label (its model),
/// and the conversation it belongs to with the folder that conversation worked in.
/// </summary>
public readonly record struct LoggedResponse(string Key, LedgerEntry Entry, string? Label = null, string? Session = null, string? Project = null);

/// <summary>Reads one log line into a response, or null when the line holds none.</summary>
public delegate LoggedResponse? ResponseReader(string path, ReadOnlySpan<byte> line);

/// <summary>The response logged last: the file it came from, when, and its label.</summary>
public sealed record LatestResponse(string Path, long At, string? Label);

/// <summary>Where a conversation was logged last, and what its log says of it.</summary>
public sealed record SessionSource(string Path, string? Project, string? Label);

/// <summary>A conversation's tokens, when it last answered and where it was logged.</summary>
public sealed record SessionTally(string Session, SessionSource Source, long LastAt, TokenCount Tokens);

/// <summary>
/// The tokens of every model response a provider logs one line per response, kept in a ledger of their own
/// so the totals survive the logs being cleaned up. Each JSONL file is read from where the last scan stopped,
/// and a response logged again (a fuller copy of the same one) replaces its earlier count instead of adding to it.
/// Each response also keeps the conversation it came from, so the ledger sums them per conversation too.
/// </summary>
public sealed class ResponseLedger
{
    /// <summary>Ledgers before it kept no conversation per response: they read their logs once more to learn them.</summary>
    private const int ConversationsVersion = 1;

    private readonly Lock _gate = new();
    private readonly Func<IReadOnlyList<string>> _folders;
    private readonly byte[] _marker;
    private readonly ResponseReader _read;
    private readonly LedgerStore<StoredLedger> _store;
    private readonly Dictionary<string, long> _offsets;
    private readonly Dictionary<string, LedgerEntry> _messages;
    private readonly Dictionary<string, SessionSource> _sessions;
    private LatestResponse? _latest;
    private IReadOnlyList<SessionTally>? _tallies;

    /// <param name="folders">Where the logs are, asked on every scan: sources come and go.</param>
    /// <param name="marker">Bytes every line with a response contains: other lines are passed over without parsing.</param>
    public ResponseLedger(Func<IReadOnlyList<string>> folders, string storePath, ReadOnlySpan<byte> marker, ResponseReader read)
    {
        _folders = folders;
        _marker = marker.ToArray();
        _read = read;
        _store = new LedgerStore<StoredLedger>(storePath);
        var stored = _store.Load();
        _offsets = stored?.Offsets ?? [];
        _messages = stored?.Messages ?? [];
        _sessions = stored?.Sessions ?? [];
        _latest = stored?.Latest;
        if (stored is not null && stored.Version < ConversationsVersion)
        {
            // Responses already counted learn their conversation as their logs are read again; none counts twice.
            _offsets.Clear();
            _store.Changed();
        }
    }

    public LatestResponse? Latest
    {
        get { lock (_gate) return _latest; }
    }

    public void Scan()
    {
        lock (_gate)
        {
            JsonlTail.ReadFolders(_folders(), _offsets, ReadFile);
            _store.SaveIfDue(Stored());
        }
    }

    public TokenSummary Summarize(DateTimeOffset now, DateTimeOffset shortWindowStart, DateTimeOffset longWindowStart)
    {
        lock (_gate) return TokenSummary.Of(_messages.Values, now, shortWindowStart, longWindowStart);
    }

    /// <summary>The tokens of each conversation, worked out again only after a scan counted something new.</summary>
    public IReadOnlyList<SessionTally> Sessions()
    {
        lock (_gate)
        {
            return _tallies ??= _messages.Values
                .Where(entry => entry.Session is not null)
                .GroupBy(entry => entry.Session!, StringComparer.Ordinal)
                .Select(group => new SessionTally(
                    group.Key,
                    _sessions.GetValueOrDefault(group.Key) ?? new SessionSource("", null, null),
                    group.Max(entry => entry.At),
                    group.Aggregate(default(TokenCount), (sum, entry) => sum + entry.Count)))
                .ToArray();
        }
    }

    public void Flush()
    {
        lock (_gate) _store.Flush(Stored());
    }

    private void ReadFile(string path, long length)
    {
        var offset = _offsets.GetValueOrDefault(path);
        if (offset == length) return;

        var next = JsonlTail.Read(path, offset, line => Record(path, line));
        if (next == offset) return;

        _offsets[path] = next;
        _store.Changed();
    }

    private void Record(string path, ReadOnlySpan<byte> line)
    {
        if (line.IndexOf(_marker) < 0 || _read(path, line) is not { } response) return;

        if (_latest is null || response.Entry.At >= _latest.At) _latest = new LatestResponse(path, response.Entry.At, response.Label);
        var entry = response.Session is { } session ? response.Entry with { Session = session } : response.Entry;
        if (response.Session is { } id)
        {
            var known = _sessions.GetValueOrDefault(id);
            _sessions[id] = new SessionSource(path, response.Project ?? known?.Project, response.Label ?? known?.Label);
        }

        _tallies = null;
        // A response may be logged more than once as it fills in: keep its fullest count, and learn its conversation.
        if (_messages.TryGetValue(response.Key, out var counted) && counted.Count.Total >= entry.Count.Total)
        {
            if (counted.Session is null && entry.Session is not null) _messages[response.Key] = counted with { Session = entry.Session };
            return;
        }

        _messages[response.Key] = entry;
    }

    private StoredLedger Stored() => new(_offsets, _messages, _latest, _sessions, ConversationsVersion);

    /// <summary>
    /// The file's shape: offsets by log path and the responses counted (Claude's ledger.json since 1.0.0), the latest
    /// one, and since <see cref="ConversationsVersion"/> where each conversation was logged.
    /// </summary>
    private sealed record StoredLedger(
        Dictionary<string, long> Offsets,
        Dictionary<string, LedgerEntry> Messages,
        LatestResponse? Latest = null,
        Dictionary<string, SessionSource>? Sessions = null,
        int Version = 0);
}

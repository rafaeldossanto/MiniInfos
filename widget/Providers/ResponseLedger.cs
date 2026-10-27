using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers;

/// <summary>One model response read from a log line: the key that names it, its tokens, and an optional label (its model).</summary>
public readonly record struct LoggedResponse(string Key, LedgerEntry Entry, string? Label = null);

/// <summary>Reads one log line into a response, or null when the line holds none.</summary>
public delegate LoggedResponse? ResponseReader(string path, ReadOnlySpan<byte> line);

/// <summary>The response logged last: the file it came from, when, and its label.</summary>
public sealed record LatestResponse(string Path, long At, string? Label);

/// <summary>
/// The tokens of every model response a provider logs one line per response, kept in a ledger of their own
/// so the totals survive the logs being cleaned up. Each JSONL file is read from where the last scan stopped,
/// and a response logged again (a fuller copy of the same one) replaces its earlier count instead of adding to it.
/// </summary>
public sealed class ResponseLedger
{
    private readonly Lock _gate = new();
    private readonly Func<IReadOnlyList<string>> _folders;
    private readonly byte[] _marker;
    private readonly ResponseReader _read;
    private readonly LedgerStore<StoredLedger> _store;
    private readonly Dictionary<string, long> _offsets;
    private readonly Dictionary<string, LedgerEntry> _messages;
    private LatestResponse? _latest;

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
        _latest = stored?.Latest;
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
        // A response may be logged more than once as it fills in: keep its fullest count.
        if (_messages.TryGetValue(response.Key, out var known) && known.Count.Total >= response.Entry.Count.Total) return;
        _messages[response.Key] = response.Entry;
    }

    private StoredLedger Stored() => new(_offsets, _messages, _latest);

    /// <summary>The file's shape: offsets by log path and the responses counted (Claude's ledger.json since 1.0.0), and the latest one.</summary>
    private sealed record StoredLedger(Dictionary<string, long> Offsets, Dictionary<string, LedgerEntry> Messages, LatestResponse? Latest = null);
}

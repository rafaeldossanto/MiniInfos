using System.IO;
using MiniInfos.Infrastructure.Storage;

namespace MiniInfos.Providers.Claude;

/// <summary>
/// Slow or unavailable WSL files never hold the token ledger's lock. Only parsed response metadata crosses back
/// into it; cursors live for this process, while the shared ledger persists and deduplicates the imported totals.
/// </summary>
internal sealed class WslClaudeHistory(Func<IReadOnlyList<string>> projects, Action<string, IEnumerable<LoggedResponse>> import)
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(10);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _offsets = new(StringComparer.Ordinal);
    private DateTimeOffset _startedAt = DateTimeOffset.MinValue;
    private Task _scan = Task.CompletedTask;

    public Task Refresh(bool force = false)
    {
        lock (_gate)
        {
            if (!_scan.IsCompleted || (!force && DateTimeOffset.UtcNow - _startedAt < RefreshEvery)) return _scan;
            _startedAt = DateTimeOffset.UtcNow;
            return _scan = Task.Run(Read);
        }
    }

    private void Read()
    {
        try
        {
            var files = projects().SelectMany(root => SourceFiles.FileInfos(root, "*.jsonl", recursive: true))
                .DistinctBy(file => file.FullName, StringComparer.Ordinal).OrderByDescending(file => file.Length).ToArray();
            foreach (var file in files)
            {
                var offset = _offsets.GetValueOrDefault(file.FullName);
                if (offset == file.Length) continue;
                var responses = new List<LoggedResponse>();
                var next = JsonlTail.Read(file.FullName, offset, line =>
                {
                    if (line.IndexOf("\"usage\""u8) >= 0 && ClaudeUsageLedger.Read(file.FullName, line) is { } response)
                        responses.Add(response);
                });
                if (responses.Count > 0) import(file.FullName, responses);
                _offsets[file.FullName] = next;
            }
            var seen = files.Select(file => file.FullName).ToHashSet(StringComparer.Ordinal);
            foreach (var gone in _offsets.Keys.Where(path => !seen.Contains(path)).ToArray()) _offsets.Remove(gone);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A distro can stop or deny access while it is read. The next refresh queries running distros anew.
        }
    }
}

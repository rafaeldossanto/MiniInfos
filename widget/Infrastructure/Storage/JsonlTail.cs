using System.IO;

namespace MiniInfos.Infrastructure.Storage;

/// <summary>One complete line of a JSONL file, without its newline.</summary>
public delegate void JsonlLine(ReadOnlySpan<byte> line);

/// <summary>
/// Reads JSONL logs incrementally: each file from where the last read stopped, complete lines only,
/// so a line still being written waits for its newline. What every token ledger reads its logs with.
/// </summary>
public static class JsonlTail
{
    /// <summary>Hands each complete line appended since <paramref name="offset"/> to <paramref name="onLine"/>.</summary>
    /// <param name="onRestart">Called before the lines when the file got shorter (rewritten): reading starts over.</param>
    /// <returns>The offset to read from next time; the same one when the file is busy or gone right now.</returns>
    public static long Read(string path, long offset, JsonlLine onLine, Action? onRestart = null)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < offset)
            {
                offset = 0;
                onRestart?.Invoke();
            }

            if (stream.Length == offset) return offset;

            stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[checked((int)(stream.Length - offset))];
            stream.ReadExactly(buffer);

            var end = Array.LastIndexOf(buffer, (byte)'\n');
            if (end < 0) return offset;

            var rest = buffer.AsSpan(0, end + 1);
            while (!rest.IsEmpty)
            {
                var newline = rest.IndexOf((byte)'\n');
                onLine(rest[..newline]);
                rest = rest[(newline + 1)..];
            }

            return offset + end + 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            // Busy, gone or too big right now: the next scan tries again from the same offset.
            return offset;
        }
    }

    /// <summary>Hands every JSONL file under <paramref name="root"/> to <paramref name="readFile"/>, then drops the cursors of files that are gone.</summary>
    public static void ReadFolder<TCursor>(string root, Dictionary<string, TCursor> cursors, Action<string, long> readFile)
    {
        if (!Directory.Exists(root)) return;
        ReadFolders([root], cursors, readFile);
    }

    /// <summary>One scan of all sources: no source can remove another source's cursors.</summary>
    /// <param name="readFile">
    /// Gets each file's path and its length as the listing found it, so a reader can pass over a file its cursor
    /// already reached without opening it: most logs don't change between two scans.
    /// </param>
    public static void ReadFolders<TCursor>(IReadOnlyList<string> roots, Dictionary<string, TCursor> cursors, Action<string, long> readFile)
    {
        var files = roots.SelectMany(root => SourceFiles.FileInfos(root, "*.jsonl", recursive: true))
            .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase).ToArray();
        // A full copy supplies historical deltas before a shorter, partially copied session is read.
        foreach (var file in files.OrderByDescending(file => file.Length)) readFile(file.FullName, file.Length);
        var seen = files.Select(file => file.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in cursors.Keys.Where(path => !seen.Contains(path)).ToArray()) cursors.Remove(gone);
    }
}

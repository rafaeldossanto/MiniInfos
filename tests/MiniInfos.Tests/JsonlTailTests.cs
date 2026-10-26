using System.Text;

namespace MiniInfos.Tests;

public sealed class JsonlTailTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void OnlyCompleteLinesAreHandedAndTheOffsetStopsAfterThem()
    {
        var log = _folder.File("a.jsonl");
        File.WriteAllText(log, "one\ntwo\nthr");
        var lines = new List<string>();

        var offset = JsonlTail.Read(log, 0, line => lines.Add(Encoding.UTF8.GetString(line)));

        Assert.Equal(["one", "two"], lines);
        Assert.Equal(8, offset);
    }

    [Fact]
    public void AFileThatGotShorterStartsOver()
    {
        var log = _folder.File("a.jsonl");
        File.WriteAllText(log, "new\n");
        var restarted = false;
        var lines = new List<string>();

        var offset = JsonlTail.Read(log, 100, line => lines.Add(Encoding.UTF8.GetString(line)), () => restarted = true);

        Assert.True(restarted);
        Assert.Equal(["new"], lines);
        Assert.Equal(4, offset);
    }

    [Fact]
    public void AFileThatIsGoneKeepsItsOffset() =>
        Assert.Equal(42, JsonlTail.Read(Path.Combine(_folder.Path, "missing.jsonl"), 42, _ => { }));

    [Fact]
    public void OverlappingRootsReadEachFileOnceAndKeepBothSetsOfCursors()
    {
        var a = _folder.File("a", "one.jsonl");
        var b = _folder.File("b", "two.jsonl");
        File.WriteAllText(a, "one\n");
        File.WriteAllText(b, "two\n");
        var cursors = new Dictionary<string, long> { [a] = 4, [b] = 4 };
        var read = new List<string>();

        JsonlTail.ReadFolders([Path.Combine(_folder.Path, "a"), Path.Combine(_folder.Path, "b"), _folder.Path], cursors, read.Add);

        Assert.Equal(2, read.Count);
        Assert.Equal(2, cursors.Count);
        Assert.Contains(a, read);
        Assert.Contains(b, read);
    }

    [Fact]
    public void AFolderReadDropsTheCursorsOfGoneFiles()
    {
        var kept = _folder.File("logs", "kept.jsonl");
        File.WriteAllText(kept, "");
        var cursors = new Dictionary<string, long> { [kept] = 0, [Path.Combine(_folder.Path, "logs", "gone.jsonl")] = 9 };
        var read = new List<string>();

        JsonlTail.ReadFolder(Path.Combine(_folder.Path, "logs"), cursors, read.Add);

        Assert.Equal([kept], read);
        Assert.Equal([kept], cursors.Keys);
    }
}

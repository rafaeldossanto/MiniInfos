using System.Text;
using Microsoft.Data.Sqlite;
using MiniInfos.Infrastructure.Desktop;
using MiniInfos.Providers;
using MiniInfos.Providers.Antigravity;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Providers.Antigravity;

public sealed class AntigravityUsageTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;
    private const string Session = "33047a8e-351f-4ca5-84d2-2fd0040b578b";
    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();
    private string Database => _folder.File("conversations", Session + ".db");
    private string Store => _folder.File("ledger.json");
    private AntigravityUsage Usage() => new(() => [Path.GetDirectoryName(Database)!], Store, _ => Task.FromResult<string?>(null));

    [Fact]
    public void ReadsUsageWithSeparateCachesAndDoesNotAddThinkingTwice()
    {
        var response = AntigravityResponses.Read(Session, 1, Metadata("response", 100, 35, 60, 10), "project")!.Value;
        Assert.Equal(new TokenCount(100, 35, 60, 10), response.Entry.Count);
        Assert.Equal(Now.ToUnixTimeSeconds(), response.Entry.At);
        Assert.Equal("project", response.Project);
    }

    [Fact]
    public async Task DatabaseUpdatesAndRepeatedStepsCountEachResponseOnceAndSurviveRestart()
    {
        using var db = CreateDatabase();
        Insert(db, 1, Metadata("a", 100, 20, 60, 10));
        Insert(db, 2, Metadata("a", 100, 20, 60, 10)); // The same generation attached to another step.
        Insert(db, 3, Message(1, Count(1, Now.ToUnixTimeSeconds()))); // A user/tool step with no usage.
        var usage = Usage();
        var first = await usage.ReadAsync(Now, true, false);
        Assert.Equal(190, first.Total.Total);
        Assert.Null(first.ShortWindow.Window.Limit);
        Assert.Null(first.LongWindow.Window.Limit);
        Assert.Equal("Antigravity", first.Title);
        Insert(db, 4, Metadata("b", 200, 40)); // Committed in WAL, while the application keeps its connection open.
        var next = await usage.ReadAsync(Now, true, false);
        Assert.Equal(430, next.Total.Total);
        Assert.Equal(430, (await usage.ReadAsync(Now, true, true)).Total.Total);
        usage.Flush();
        Assert.Equal(430, (await Usage().ReadAsync(Now, true, false)).Total.Total);
        Assert.DoesNotContain("private prompt", File.ReadAllText(Store));
        var chat = Assert.Single(usage.Conversations());
        Assert.Equal(("antigravity", Session, 430L), (chat.ProviderId, chat.Key, chat.Tokens.Total));
        Assert.Equal("project", chat.Title);
    }

    [Fact]
    public async Task FailedOrRemovedDatabasesNeverErasePreviouslyCountedUsage()
    {
        using (var db = CreateDatabase()) Insert(db, 1, Metadata("a", 10, 5));
        var usage = Usage();
        Assert.Equal(15, (await usage.ReadAsync(Now, true, false)).Total.Total);
        File.WriteAllText(Database, "not sqlite");
        Assert.Equal(15, (await usage.ReadAsync(Now, true, true)).Total.Total);
        File.Delete(Database);
        Assert.Equal(15, (await usage.ReadAsync(Now, true, false)).Total.Total);
    }

    [Fact]
    public async Task ReadOnlyScanLeavesTheDatabaseUnchangedAndMissingSourcesProduceAnEmptyPage()
    {
        var empty = await Usage().ReadAsync(Now, true, false);
        Assert.Equal(0, empty.Total.Total);
        Assert.False(File.Exists(Database));
        using (var db = CreateDatabase()) Insert(db, 1, Metadata("a", 10, 5));
        var before = File.ReadAllBytes(Database);
        Assert.Equal(15, (await Usage().ReadAsync(Now, true, false)).Total.Total);
        Assert.Equal(before, File.ReadAllBytes(Database));
    }

    [Fact]
    public void UnknownFieldsAreIgnoredAndMalformedMetadataIsSkipped()
    {
        var valid = Metadata("a", 10, 5);
        Assert.NotNull(AntigravityResponses.Read(Session, 0, [.. valid, .. Message(100, [1, 2])], null));
        foreach (var bad in new byte[][] { [0], [0x4a, 0xff], [.. valid, 0x80], [0x0f], Message(9, [0x12, 0xff]) })
            Assert.Null(AntigravityResponses.Read(Session, 0, bad, null));
        Assert.Null(AntigravityResponses.Read(Session, 0, Metadata("a", long.MaxValue, 1), null));
    }

    [Fact]
    public void DiscoveryUsesTheAgentFolderAndInstallationDoesNotDependOnGeminiCli()
    {
        Directory.CreateDirectory(_folder.File(".gemini", "antigravity", "conversations"));
        Assert.Single(AntigravityPaths.Find(_folder.Path));
        Assert.DoesNotContain("gemini", AntigravityInstall.Footprint.Commands);
        Assert.Contains("Antigravity", AntigravityInstall.Footprint.Processes);
        Assert.False(InstalledApps.Has(AntigravityInstall.Footprint with { Files = [], Bundles = [] },
            new InstallContext(_folder.Path, "", _ => false, _ => false)));
    }

    private SqliteConnection CreateDatabase()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE steps(idx INTEGER PRIMARY KEY, metadata BLOB, step_payload TEXT); CREATE TABLE trajectory_metadata_blob(id TEXT PRIMARY KEY,data BLOB);";
        command.ExecuteNonQuery();
        command.CommandText = "INSERT INTO trajectory_metadata_blob VALUES('main',$data)";
        command.Parameters.AddWithValue("$data", Message(1, Message(1, Encoding.UTF8.GetBytes("file:///workspace/project"))));
        command.ExecuteNonQuery();
        return db;
    }

    private static void Insert(SqliteConnection db, int index, byte[] metadata)
    {
        using var command = db.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO steps VALUES($idx,$metadata,'private prompt')";
        command.Parameters.AddWithValue("$idx", index); command.Parameters.AddWithValue("$metadata", metadata);
        command.ExecuteNonQuery();
    }

    private static byte[] Metadata(string id, long input, long output, long read = 0, long write = 0) => [
        .. Message(1, Count(1, Now.ToUnixTimeSeconds())),
        .. Message(9, [.. Count(2, input), .. Count(3, output), .. Count(5, read), .. Count(4, write),
            .. Count(9, 20), .. Count(10, 15), .. Message(7, Encoding.UTF8.GetBytes(id))]),
    ];
    private static byte[] Message(int field, byte[] content) => [.. Varint((ulong)(field * 8 + 2)), .. Varint((ulong)content.Length), .. content];
    private static byte[] Count(int field, long value) => [.. Varint((ulong)(field * 8)), .. Varint((ulong)value)];
    private static byte[] Varint(ulong n)
    {
        var bytes = new List<byte>();
        while (n >= 128) { bytes.Add((byte)((n & 127) | 128)); n >>= 7; }
        bytes.Add((byte)n); return bytes.ToArray();
    }
}

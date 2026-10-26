using System.IO.Compression;
using System.Text.Json;
using MiniInfos.Community;
using MiniInfos.Mascot;
using MiniInfos.Settings;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Community;

public sealed class PackageLibraryTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private PackageLibrary Library => new(Path.Combine(_folder.Path, "library"));
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void ThemeCanBeInstalledUpdatedExportedAndRemoved()
    {
        var archive = _folder.File("ocean.miniinfos");
        PackageLibrary.Create(archive, PackageFixture.Theme(), new Dictionary<string, byte[]>());
        var installed = Library.Install(archive, PackageKind.Theme);
        Assert.Equal("Oceano", Library.List(PackageKind.Theme).Single().Name);
        Assert.Equal("Comunidade", installed.Manifest.Author);

        PackageLibrary.Create(archive, PackageFixture.Theme(version: "2.0.0"), new Dictionary<string, byte[]>());
        Library.Install(archive, PackageKind.Theme);
        Assert.Equal("2.0.0", Library.Find(PackageKind.Theme, installed.Id)!.Manifest.Version);
        var exported = _folder.File("shared.miniinfos");
        Library.Export(PackageKind.Theme, installed.Id, exported);
        var other = new PackageLibrary(Path.Combine(_folder.Path, "other-library"));
        Assert.Equal(installed.Id, other.Install(exported, PackageKind.Theme).Id);
        Library.Remove(PackageKind.Theme, installed.Id);
        Assert.Null(Library.Find(PackageKind.Theme, installed.Id));
    }

    [Fact]
    public void InvalidUpdatePreservesTheInstalledVersion()
    {
        var archive = _folder.File("ocean.miniinfos");
        PackageLibrary.Create(archive, PackageFixture.Theme(), new Dictionary<string, byte[]>());
        Library.Install(archive, PackageKind.Theme);
        PackageLibrary.Create(archive, PackageFixture.Theme(version: "2.0.0") with { SchemaVersion = 99 }, new Dictionary<string, byte[]>());
        Assert.Throws<PackageException>(() => Library.Install(archive, PackageKind.Theme));
        Assert.Equal("1.0.0", Library.Find(PackageKind.Theme, "test.ocean")!.Manifest.Version);
    }

    [Fact]
    public void MascotTracksLoadTheirOwnTimingsAndCanFallbackToIdle()
    {
        var archive = _folder.File("friend.miniinfos");
        PackageLibrary.Create(archive, PackageFixture.Mascot(), new Dictionary<string, byte[]> { ["sprites.png"] = PackageFixture.Png() });
        Library.Install(archive, PackageKind.Mascot);
        var appearance = PetAppearances.Load("test.friend", Library);
        Assert.Equal("Companheiro", appearance.Name);
        Assert.Equal(0, appearance.PoseAt(PetState.Idle, TimeSpan.FromMilliseconds(40)).From);
        Assert.Equal(1, appearance.PoseAt(PetState.Idle, TimeSpan.FromMilliseconds(100)).From);
        Assert.Equal(appearance.PoseAt(PetState.Idle, TimeSpan.FromMilliseconds(100)), appearance.PoseAt(PetState.Alert, TimeSpan.FromMilliseconds(100)));
        Assert.False(appearance.HasPose(PetState.HeldLeftWing));
        Assert.Same(PetAppearances.Athena, PetAppearances.Load("unknown.pet", Library));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("C:/outside.txt")]
    [InlineData("/outside.txt")]
    [InlineData("folder/../../outside.txt")]
    [InlineData("plugin.dll")]
    [InlineData("theme.xaml")]
    [InlineData("aux.txt")]
    [InlineData("some/LPT1.json")]
    public void ArchivePathsAndExecutableContentAreRejected(string entryName)
    {
        var archive = _folder.File("invalid.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write("invalid");
        }
        Assert.Throws<PackageException>(() => Library.Install(archive, PackageKind.Theme));
        Assert.False(File.Exists(Path.Combine(_folder.Path, "outside.txt")));
    }

    [Fact]
    public void DuplicateCaseInsensitivePathsAreRejected()
    {
        var archive = _folder.File("duplicate.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            zip.CreateEntry("manifest.json"); zip.CreateEntry("MANIFEST.JSON");
        }
        Assert.Throws<PackageException>(() => Library.Install(archive, PackageKind.Theme));
    }

    [Fact]
    public void OversizedEntryIsRejectedBeforeDecompression()
    {
        var archive = _folder.File("large.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        using (var stream = zip.CreateEntry("large.txt", CompressionLevel.Optimal).Open())
        {
            var block = new byte[1024 * 1024];
            for (var megabyte = 0; megabyte < 33; megabyte++) stream.Write(block);
        }
        var error = Assert.Throws<PackageException>(() => Library.Install(archive, PackageKind.Theme));
        Assert.Contains("limite", error.Message);
        Assert.Empty(Library.List(PackageKind.Theme));
    }

    [Fact]
    public void TooManyEntriesAreRejectedAndMissingManifestIsExplained()
    {
        var archive = _folder.File("many.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            for (var index = 0; index < 129; index++) zip.CreateEntry($"file-{index}.txt");
        Assert.Contains("128", Assert.Throws<PackageException>(() => Library.Install(archive, PackageKind.Theme)).Message);
        var empty = _folder.File("empty.zip");
        using (ZipFile.Open(empty, ZipArchiveMode.Create)) { }
        Assert.Contains("manifest.json", Assert.Throws<PackageException>(() => Library.Install(empty, PackageKind.Theme)).Message);
    }

    [Fact]
    public void WrongPackageKindDoesNotInstallAnything()
    {
        var archive = _folder.File("ocean.miniinfos");
        PackageLibrary.Create(archive, PackageFixture.Theme(), new Dictionary<string, byte[]>());
        Assert.Throws<PackageException>(() => Library.Install(archive, PackageKind.Mascot));
        Assert.Empty(Library.List(PackageKind.Theme));
        Assert.Empty(Library.List(PackageKind.Mascot));
    }

    [Fact]
    public void AnatomyAndPreferencesRoundtripWithoutChangingExistingDefaults()
    {
        var manifest = PackageFixture.Mascot() with { Mascot = PackageFixture.Mascot().Mascot! with { Anatomy = PetAnatomy.Athena } };
        var restored = JsonSerializer.Deserialize<PackageManifest>(JsonSerializer.Serialize(manifest, PackageManifest.Json), PackageManifest.Json)!;
        Assert.Equal(PetAnatomy.Athena.MassCenter, restored.Mascot!.Anatomy!.MassCenter);
        var path = _folder.File("settings.json");
        var preferences = new Preferences(path) { CommunityThemeId = "test.ocean", PetId = "test.friend" };
        var loaded = new Preferences(path);
        Assert.Equal(("test.ocean", "test.friend"), (loaded.CommunityThemeId, loaded.PetId));
        Assert.Equal((ThemeChoice.Simple, PetSizeChoice.Medium), (loaded.Theme, loaded.PetSize));
        preferences.Theme = ThemeChoice.Simple;
        Assert.Null(preferences.CommunityThemeId);
        Assert.Equal("test.friend", preferences.PetId);
    }

    [Theory]
    [InlineData("miniinfos.builtin")]
    [InlineData("../bad")]
    [InlineData("con.theme")]
    [InlineData("author.theme.")]
    public void InvalidOrReservedIdsCannotReachTheLibrary(string id) => Assert.Throws<PackageException>(() => PackageValidation.Id(id));
}

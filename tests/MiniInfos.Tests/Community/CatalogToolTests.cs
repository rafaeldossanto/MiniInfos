using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MiniInfos.Catalog;
using MiniInfos.Community;
using MiniInfos.Community.Catalog;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Community;

public sealed class CatalogToolTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private static readonly DateTimeOffset BuiltAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private string Sources => Path.Combine(_folder.Path, "packages");
    public void Dispose() => _folder.Dispose();

    [Fact]
    public void TwoBuildsOfTheSameSourcesProduceIdenticalPackagesPreviewsAndIndex()
    {
        Source(PackageFixture.Theme());
        Source(PackageFixture.Mascot());
        var first = Path.Combine(_folder.Path, "first");
        var second = Path.Combine(_folder.Path, "second");
        var builder = new CatalogBuilder(() => BuiltAt);
        var index = builder.Build(Sources, first);
        builder.Build(Sources, second);
        foreach (var file in Directory.GetFiles(first))
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(file)), SHA256.HashData(File.ReadAllBytes(Path.Combine(second, Path.GetFileName(file)))));
        foreach (var entry in index.Packages)
        {
            Assert.Equal((128, 128), PackageImages.Dimensions(Path.Combine(first, entry.Preview)));
            Assert.Equal(entry.Size, new FileInfo(Path.Combine(first, entry.Asset)).Length);
            Assert.Equal(entry.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(first, entry.Asset)))).ToLowerInvariant());
            using var zip = ZipFile.OpenRead(Path.Combine(first, entry.Asset));
            Assert.Equal(zip.Entries.Select(item => item.FullName).Order(StringComparer.Ordinal), zip.Entries.Select(item => item.FullName));
            Assert.All(zip.Entries, item => Assert.Equal(1980, item.LastWriteTime.Year));
        }
        Assert.Equal(2, CatalogIndex.Read(File.ReadAllBytes(Path.Combine(first, "index.json"))).Packages.Length);
    }

    [Fact]
    public void ValidationRejectsAnIdOutsideItsFolderNameAndAnUnlicensedPackage()
    {
        var wrong = Source(PackageFixture.Theme(), "different.folder");
        Assert.Contains("mesmo nome", Assert.Throws<PackageException>(() => new CatalogValidator().Validate(wrong)).Message);
        var unlicensed = Source(PackageFixture.Theme() with { License = "UNLICENSED" });
        Assert.Contains("licença", Assert.Throws<PackageException>(() => new CatalogValidator().Validate(unlicensed)).Message);
    }

    [Theory]
    [InlineData("1.0.0", false)]
    [InlineData("0.9.0", false)]
    [InlineData("1.0.1", true)]
    public void PreviousIndexRequiresANumericallyGreaterVersion(string version, bool accepted)
    {
        var manifest = PackageFixture.Theme();
        Source(manifest);
        var builder = new CatalogBuilder(() => BuiltAt);
        var previous = builder.Build(Sources, Path.Combine(_folder.Path, "old"));
        Source(manifest with { Version = version });
        var output = Path.Combine(_folder.Path, "next");
        if (accepted) Assert.Equal(version, builder.Build(Sources, output, previous).Packages.Single().Version);
        else
        {
            Assert.Contains("maior", Assert.Throws<PackageException>(() => builder.Build(Sources, output, previous)).Message);
            Assert.False(Directory.Exists(output));
        }
    }

    [Fact]
    public void TheCatalogRejectsPackagesAboveEightCompressedMegabytes()
    {
        var source = Source(PackageFixture.Theme());
        var bytes = new byte[8 * 1024 * 1024 + 4096];
        new Random(497).NextBytes(bytes);
        File.WriteAllBytes(Path.Combine(source, "noise.txt"), bytes);
        Assert.Contains("8 MB", Assert.Throws<PackageException>(() => new CatalogValidator().Validate(source)).Message);
    }

    [Fact]
    public void CommandFailuresUseNonzeroExitCodesAndPortugueseMessages()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CatalogCommand.Run(["build"], output, error));
        Assert.Contains("Uso:", error.ToString());
        var source = Source(PackageFixture.Theme() with { License = "UNLICENSED" });
        Assert.Equal(1, CatalogCommand.Run(["validate", source], output, error));
        Assert.Contains("licença", error.ToString());
        Source(PackageFixture.Theme());
        Assert.Equal(0, CatalogCommand.Run(["validate", source], output, error));
    }

    [Fact]
    public void OutputCannotBeWrittenInsideTheSourceTree()
    {
        Source(PackageFixture.Theme());
        Assert.Contains("fora", Assert.Throws<PackageException>(() =>
            new CatalogBuilder().Build(Sources, Path.Combine(Sources, "output"))).Message);
    }

    [Fact]
    public void MalformedIndexesAndDuplicateIdsAreRejectedBeforeTheyCanGuideABuild()
    {
        Assert.Throws<PackageException>(() => CatalogIndex.Read(new byte[CatalogIndex.MaxBytes + 1]));
        Assert.Throws<PackageException>(() => CatalogIndex.Read("{\"schemaVersion\":1,\"extra\":true}"u8));
        Source(PackageFixture.Theme());
        var index = new CatalogBuilder(() => BuiltAt).Build(Sources, Path.Combine(_folder.Path, "out"));
        Assert.Throws<PackageException>(() => (index with { Packages = [index.Packages[0], index.Packages[0]] }).Validate());
        Assert.Throws<PackageException>(() => (index with { Packages = [index.Packages[0] with { Sha256 = "broken" }] }).Validate());
        Assert.Throws<PackageException>(() => (index with { Packages = [index.Packages[0] with { Asset = "../wrong.miniinfos" }] }).Validate());
    }

    private string Source(PackageManifest manifest, string? folderName = null)
    {
        var folder = Path.Combine(Sources, folderName ?? manifest.Id);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, PackageManifest.Json));
        if (manifest.Mascot is not null) File.WriteAllBytes(Path.Combine(folder, manifest.Mascot.SpriteSheet), PackageFixture.Png());
        return folder;
    }
}

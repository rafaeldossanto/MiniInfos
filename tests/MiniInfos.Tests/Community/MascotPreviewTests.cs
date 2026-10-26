using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiniInfos.Community;
using MiniInfos.Mascot;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Community;

public sealed class MascotPreviewTests : IDisposable
{
    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task TwentyCachedMascotsOpenWithoutDecodingAnySpriteSheet()
    {
        var cacheRoot = Path.Combine(_folder.Path, "previews");
        var library = new PackageLibrary(Path.Combine(_folder.Path, "packages"), new(cacheRoot));
        for (var index = 0; index < 20; index++)
            Install(library, PackageFixture.Mascot($"test.friend-{index}"));
        var decodes = 0;
        var reopened = new MascotPreviewCache(cacheRoot, package =>
        {
            Interlocked.Increment(ref decodes);
            return PackagePreviews.Mascot(package);
        });
        var images = await Task.WhenAll(library.List(PackageKind.Mascot).Select(reopened.GetAsync));
        Assert.Equal(0, decodes);
        Assert.Equal(20, images.Length);
        Assert.All(images, image => Assert.True(image is { IsFrozen: true, PixelWidth: 128, PixelHeight: 128 }));
    }

    [Fact]
    public async Task MissingPreviewIsGeneratedOnceOffTheCallingThreadAndSharedByConcurrentRequests()
    {
        var library = new PackageLibrary(Path.Combine(_folder.Path, "packages"));
        var package = Install(library, PackageFixture.Mascot());
        var decodes = 0;
        var cache = new MascotPreviewCache(Path.Combine(_folder.Path, "new-cache"), item =>
        {
            Interlocked.Increment(ref decodes);
            Assert.True(Thread.CurrentThread.IsThreadPoolThread);
            return PackagePreviews.Mascot(item);
        });
        var images = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetAsync(package)));
        Assert.Equal(1, decodes);
        Assert.All(images, image => Assert.Same(images[0], image));
        Assert.True(File.Exists(Path.Combine(cache.Root, $"{package.Id}-{package.Manifest.Version}.png")));
    }

    [Fact]
    public async Task IdleArtworkIsCroppedAndCenteredInsideThe128PixelPreview()
    {
        var pixels = new byte[320 * 320 * 4];
        for (var y = 180; y < 240; y++)
        for (var x = 150; x < 190; x++)
            pixels[(y * 320 + x) * 4 + 3] = 255;
        var image = BitmapSource.Create(320, 320, 96, 96, PixelFormats.Pbgra32, null, pixels, 320 * 4);
        image.Freeze();
        var manifest = PackageFixture.Mascot() with { Mascot = new("sprites.png", 1, 1, new() { [PetState.Idle] = new(0) }) };
        var library = new PackageLibrary(Path.Combine(_folder.Path, "packages"));
        var package = Install(library, manifest, PackageImages.Encode(image));
        var preview = (await library.Previews.GetAsync(package))!;
        var result = new byte[128 * 128 * 4];
        preview.CopyPixels(result, 128 * 4, 0);
        var bounds = PetPixels.Bounds(result, 128);
        Assert.InRange(bounds.Height, 110, 114);
        Assert.InRange(bounds.Width, 73, 77);
        Assert.InRange(bounds.X + bounds.Width / 2, 63, 65);
        Assert.InRange(bounds.Y + bounds.Height / 2, 63, 65);
    }

    [Fact]
    public async Task ReimportingTheSameVersionReplacesItsPreviewAndExportsNoCacheFiles()
    {
        var library = new PackageLibrary(Path.Combine(_folder.Path, "packages"));
        var manifest = PackageFixture.Mascot();
        var package = Install(library, manifest);
        var original = await library.Previews.GetAsync(package);
        package = Install(library, manifest);
        Assert.NotSame(original, await library.Previews.GetAsync(package));
        var archive = _folder.File("export.miniinfos");
        library.Export(PackageKind.Mascot, package.Id, archive);
        using var zip = ZipFile.OpenRead(archive);
        Assert.Equal(new[] { "manifest.json", "sprites.png" }, zip.Entries.Select(entry => entry.FullName).Order());
        library.Remove(PackageKind.Mascot, package.Id);
        Assert.Empty(Directory.GetFiles(library.Previews.Root, "*.png"));
    }

    [Fact]
    public async Task CorruptCachesAreRebuiltAndANewVersionUsesItsOwnPreview()
    {
        var library = new PackageLibrary(Path.Combine(_folder.Path, "packages"));
        var manifest = PackageFixture.Mascot();
        var package = Install(library, manifest);
        var path = Path.Combine(library.Previews.Root, $"{package.Id}-{manifest.Version}.png");
        File.WriteAllText(path, "broken PNG");
        var reopened = new MascotPreviewCache(library.Previews.Root);
        Assert.NotNull(await reopened.GetAsync(package));
        Assert.Equal((128, 128), PackageImages.Dimensions(path));
        package = Install(library, manifest with { Version = "2.0.0" });
        Assert.NotNull(await reopened.GetAsync(package));
        Assert.True(File.Exists(Path.Combine(library.Previews.Root, $"{package.Id}-2.0.0.png")));
    }

    private InstalledPackage Install(PackageLibrary library, PackageManifest manifest, byte[]? png = null)
    {
        var archive = _folder.File("source.miniinfos");
        PackageLibrary.Create(archive, manifest, new Dictionary<string, byte[]> { ["sprites.png"] = png ?? PackageFixture.Png() });
        return library.Install(archive, PackageKind.Mascot);
    }
}

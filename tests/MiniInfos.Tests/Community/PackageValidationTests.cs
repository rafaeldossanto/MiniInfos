using System.Text.Json;
using MiniInfos.Community;
using MiniInfos.Mascot;
using MiniInfos.Tests.Support;

namespace MiniInfos.Tests.Community;

public sealed class PackageValidationTests : IDisposable
{
    private readonly TempFolder _folder = new();
    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData("UnknownBrush", "#112233")]
    [InlineData("Accent", "red")]
    [InlineData("Accent", "{DynamicResource Something}")]
    public void ThemeOnlyAcceptsKnownColorKeysAndLiteralColors(string key, string value)
    {
        var manifest = PackageFixture.Theme() with { Theme = new(Colors: new() { [key] = value }) };
        Assert.Throws<PackageException>(() => PackageValidation.Validate(manifest, _folder.Path));
    }

    [Fact]
    public void MissingRequiredMetadataAndUnknownFieldsAreRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PackageManifest>("{}", PackageManifest.Json));
        var json = JsonSerializer.Serialize(PackageFixture.Theme(), PackageManifest.Json);
        json = json.Insert(json.LastIndexOf('}'), ",\"script\":\"run.exe\"");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PackageManifest>(json, PackageManifest.Json));
    }

    [Fact]
    public void InvalidSpriteGridAndTimingsAreRejected()
    {
        File.WriteAllBytes(_folder.File("sprites.png"), PackageFixture.Png());
        var manifest = PackageFixture.Mascot();
        Assert.Throws<PackageException>(() => PackageValidation.Validate(manifest with { Mascot = manifest.Mascot! with { Columns = 3 } }, _folder.Path));
        var invalid = manifest.Mascot! with { States = new() { [PetState.Idle] = new(0, Frames: [7]) } };
        Assert.Throws<PackageException>(() => PackageValidation.Validate(manifest with { Mascot = invalid }, _folder.Path));
        invalid = manifest.Mascot! with { States = new() { [PetState.Idle] = new(0, DurationsMs: [1, 1]) } };
        Assert.Throws<PackageException>(() => PackageValidation.Validate(manifest with { Mascot = invalid }, _folder.Path));
    }

    [Fact]
    public void IdleIsRequiredButInteractionPosesAreOptional()
    {
        File.WriteAllBytes(_folder.File("sprites.png"), PackageFixture.Png());
        var manifest = PackageFixture.Mascot();
        PackageValidation.Validate(manifest, _folder.Path);
        var missing = manifest.Mascot! with { States = new() { [PetState.Active] = new(0) } };
        Assert.Throws<PackageException>(() => PackageValidation.Validate(manifest with { Mascot = missing }, _folder.Path));
    }

    [Fact]
    public void OversizedPngIsRejectedBeforeDecoding()
    {
        var bytes = PackageFixture.Png();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 8192);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 8192);
        var image = _folder.File("large.png");
        File.WriteAllBytes(image, bytes);
        Assert.Throws<PackageException>(() => PackageImages.Dimensions(image));
    }

    [Fact]
    public void PublishedOceanExampleFollowsTheRuntimeContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "community", "examples"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var folder = Path.Combine(directory.FullName, "community", "examples", "ocean");
        var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(Path.Combine(folder, "manifest.json")), PackageManifest.Json)!;
        PackageValidation.Validate(manifest, folder);
        Assert.Equal(PackageKind.Theme, manifest.Kind);
    }

    [Fact]
    public void CustomHitRegionsAndMissingPosesKeepTheActualGrip()
    {
        var anatomy = PetAnatomy.Athena with { Regions = [new(PetBodyPart.LeftEye, 40, 40, 15, 15)] };
        var interaction = new PetInteraction(anatomy, _ => false);
        interaction.Press(new(40, 40), new(100, 100), TimeSpan.Zero);
        interaction.Release(TimeSpan.FromMilliseconds(100));
        Assert.Equal(PetState.ProtectEyes, interaction.StateAt(TimeSpan.FromMilliseconds(120)));
        interaction.Press(new(90, 245), new(100, 100), TimeSpan.FromSeconds(1));
        interaction.Move(new(120, 100), TimeSpan.FromSeconds(1.1));
        Assert.Equal(new System.Windows.Point(90, 245), interaction.HoldAt(TimeSpan.FromSeconds(2))!.Value.Anchor);
    }
}

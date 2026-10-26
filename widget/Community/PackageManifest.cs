using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniInfos.Community;

[JsonConverter(typeof(JsonStringEnumConverter<PackageKind>))]
public enum PackageKind { Theme, Mascot }

/// <summary>The versioned, declarative contract shared by creators and the application.</summary>
public sealed record PackageManifest(
    int SchemaVersion,
    PackageKind Kind,
    string Id,
    string Name,
    string Author,
    string Version,
    string License,
    string? Description = null,
    ThemeDefinition? Theme = null,
    MascotDefinition? Mascot = null,
    [property: JsonPropertyName("$schema")] string? Schema = null)
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
}

public sealed record InstalledPackage(PackageManifest Manifest, string Directory)
{
    public string Id => Manifest.Id;
    public string Name => Manifest.Name;
    public string Attribution => $"{Manifest.Author} · v{Manifest.Version} · {Manifest.License}";
}

public sealed class PackageException(string message, Exception? inner = null) : Exception(message, inner);

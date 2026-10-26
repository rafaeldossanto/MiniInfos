using System.Text.Json;

namespace MiniInfos.Community.Catalog;

public sealed record CatalogEntry(string Id, PackageKind Kind, string Name, string Author, string Version,
    string License, string Description, string Asset, long Size, string Sha256, string Preview, string PreviewSha256);

/// <summary>The versioned release contract shared by catalog publishing and the application.</summary>
public sealed record CatalogIndex(int SchemaVersion, DateTimeOffset GeneratedAt, CatalogEntry[] Packages)
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxEntries = 2000;
    public const long MaxPackageBytes = 8 * 1024 * 1024;

    public static CatalogIndex Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxBytes) throw new PackageException("O índice do catálogo excede 1 MB.");
        try
        {
            var index = JsonSerializer.Deserialize<CatalogIndex>(bytes, PackageManifest.Json)
                ?? throw new PackageException("O índice do catálogo está vazio.");
            index.Validate();
            return index;
        }
        catch (JsonException e) { throw new PackageException("O índice do catálogo não segue o formato esperado.", e); }
    }

    public byte[] Encode()
    {
        Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, PackageManifest.Json);
        if (bytes.Length > MaxBytes) throw new PackageException("O índice do catálogo excede 1 MB.");
        return bytes;
    }

    public void Validate()
    {
        if (SchemaVersion != 1) throw new PackageException("Versão do catálogo não suportada. Este MiniInfos aceita schemaVersion 1.");
        if (GeneratedAt == default || Packages is null || Packages.Length > MaxEntries)
            throw new PackageException("O catálogo precisa de data e aceita até 2000 pacotes.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Packages)
        {
            if (entry is null) throw new PackageException("Uma entrada do catálogo está vazia.");
            PackageValidation.Id(entry.Id);
            if (!ids.Add(entry.Id)) throw new PackageException($"ID duplicado no catálogo: {entry.Id}.");
            if (!Enum.IsDefined(entry.Kind)) throw new PackageException("Tipo de pacote desconhecido no catálogo.");
            PackageValidation.ParseVersion(entry.Version);
            PackageValidation.Text(entry.Name, "nome", 80);
            PackageValidation.Text(entry.Author, "autor", 80);
            PackageValidation.Text(entry.License, "licença", 100);
            if (entry.License.Trim().Equals("UNLICENSED", StringComparison.OrdinalIgnoreCase))
                throw new PackageException("Informe uma licença para publicar no catálogo; UNLICENSED é apenas um marcador.");
            if (entry.Description is null || entry.Description.Length > 600)
                throw new PackageException("A descrição do catálogo deve ter até 600 caracteres.");
            if (entry.Size is <= 0 or > MaxPackageBytes) throw new PackageException("Um pacote do catálogo excede 8 MB ou tem tamanho inválido.");
            if (!Hash(entry.Sha256) || !Hash(entry.PreviewSha256)) throw new PackageException("SHA-256 inválido no catálogo.");
            if (entry.Asset != $"{entry.Id}-{entry.Version}.miniinfos" || entry.Preview != $"{entry.Id}-{entry.Version}.png")
                throw new PackageException("Os arquivos do catálogo precisam usar o ID e a versão do pacote.");
        }
    }

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

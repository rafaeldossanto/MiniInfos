using System.IO;
using System.Text.RegularExpressions;
using MiniInfos.Mascot;

namespace MiniInfos.Community;

public static partial class PackageValidation
{
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,79}$", RegexOptions.CultureInvariant)] private static partial Regex IdPattern();
    [GeneratedRegex("^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.CultureInvariant)] private static partial Regex ColorPattern();

    public static void Id(string id)
    {
        if (id is null || !IdPattern().IsMatch(id) || id.StartsWith("miniinfos.", StringComparison.Ordinal)
            || id.EndsWith('.') || id is "con" or "prn" or "aux" or "nul"
            || Regex.IsMatch(id, "^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\\.|$)", RegexOptions.IgnoreCase))
            throw new PackageException(L.Text("Use um ID como autor.meu-pacote, com letras minúsculas, números, pontos, hífens ou sublinhados."));
    }

    public static void Validate(PackageManifest manifest, string folder, bool decodeImages = false)
    {
        if (manifest.SchemaVersion != 1) throw new PackageException(L.Text("Versão do formato não suportada. Este MiniInfos aceita schemaVersion 1."));
        if (!Enum.IsDefined(manifest.Kind)) throw new PackageException(L.Text("Tipo de pacote desconhecido."));
        Id(manifest.Id);
        Text(manifest.Name, L.Text("nome"), 80);
        Text(manifest.Author, L.Text("autor"), 80);
        Text(manifest.License, L.Text("licença"), 100);
        if (manifest.Description?.Length > 600) throw new PackageException(L.Text("A descrição deve ter até 600 caracteres."));
        ParseVersion(manifest.Version);
        if (manifest.Kind == PackageKind.Theme)
        {
            if (manifest.Theme is not { } theme || manifest.Mascot is not null) throw new PackageException(L.Text("O pacote de tema precisa de uma definição theme."));
            if (theme.Base is not ("Simple" or "Light" or "Classic")) throw new PackageException(L.Text("O tema base precisa ser Simple, Light ou Classic."));
            foreach (var (key, value) in theme.Colors ?? [])
                if (!ThemeDefinition.ColorKeys.Contains(key) || value is null || !ColorPattern().IsMatch(value))
                    throw new PackageException(L.Format("Cor desconhecida ou inválida: {0}. Use #RRGGBB ou #AARRGGBB.", key));
            if (!double.IsFinite(theme.HeaderHeight) || theme.HeaderHeight is < 44 or > 160
                || !double.IsFinite(theme.HeaderTitleSize) || theme.HeaderTitleSize is < 10 or > 32)
                throw new PackageException(L.Text("O cabeçalho precisa ter entre 44 e 160; o título, entre 10 e 32."));
            if (theme.HeaderImage is { } image) Image(folder, image, decodeImages);
        }
        else
        {
            if (manifest.Mascot is not { } mascot || manifest.Theme is not null) throw new PackageException(L.Text("O pacote de mascote precisa de uma definição mascot."));
            if (mascot.Columns is < 1 or > 16 || mascot.Rows is < 1 or > 10) throw new PackageException(L.Text("A grade aceita de 1 a 16 colunas e de 1 a 10 linhas."));
            var (width, height) = Image(folder, mascot.SpriteSheet, decodeImages);
            if (width % mascot.Columns != 0 || height % mascot.Rows != 0) throw new PackageException(L.Text("As dimensões da imagem precisam ser divisíveis pela grade."));
            if (mascot.States is null || !mascot.States.ContainsKey(PetState.Idle)) throw new PackageException(L.Text("O mascote precisa da animação Idle."));
            foreach (var (state, track) in mascot.States)
            {
                if (!Enum.IsDefined(state) || track is null || track.Row < 0 || track.Row >= mascot.Rows)
                    throw new PackageException(L.Text("Uma animação aponta para uma linha inexistente."));
                var count = track.Frames?.Length ?? mascot.Columns;
                if (count is < 1 or > 32 || track.Frames?.Any(column => column < 0 || column >= mascot.Columns) == true)
                    throw new PackageException(L.Text("Uma animação contém quadros fora da grade."));
                if (track.FrameDurationMs is < 16 or > 60000 || track.BlendMs is < 0 or > 60000
                    || (track.DurationsMs is { } durations && (durations.Length != count || durations.Any(ms => ms is < 16 or > 60000))))
                    throw new PackageException(L.Text("Cada quadro deve durar de 16 a 60000 ms; durationsMs precisa ter um valor por quadro."));
            }
            if (mascot.Anatomy is { } anatomy)
            {
                if (anatomy.Regions is null || anatomy.Regions.Length > 32 || anatomy.Anchors is null
                    || !Point(anatomy.MassCenter) || !Point(anatomy.UpsideDownMassCenter)) throw new PackageException(L.Text("Anatomia do mascote inválida."));
                if (anatomy.Anchors.Any(pair => !Enum.IsDefined(pair.Key) || !Point(pair.Value))
                    || anatomy.Regions.Any(region => region is null || !Enum.IsDefined(region.Part) || !Point(new(region.X, region.Y))
                        || !double.IsFinite(region.RadiusX) || !double.IsFinite(region.RadiusY)
                        || region.RadiusX is <= 0 or > 640 || region.RadiusY is <= 0 or > 640))
                    throw new PackageException(L.Text("Os pontos do mascote devem ficar no canvas de 320 unidades e ter raios positivos."));
            }
        }
    }

    private static bool Point(PetPoint? point) => point is not null && double.IsFinite(point.X) && double.IsFinite(point.Y)
        && point.X is >= 0 and <= 320 && point.Y is >= 0 and <= 320;

    public static void Text(string? value, string name, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new PackageException(L.Format("Informe {0} com até {1} caracteres.", name, maximum));
    }

    public static Version ParseVersion(string? value)
    {
        if (!Version.TryParse(value, out var version) || value!.Length > 40)
            throw new PackageException(L.Text("Informe uma versão como 1.0.0."));
        return version;
    }

    private static (int Width, int Height) Image(string folder, string relative, bool decode)
    {
        var path = PackageFiles.Resolve(folder, relative);
        if (!File.Exists(path)) throw new PackageException(L.Format("Imagem não encontrada: {0}", relative));
        var dimensions = PackageImages.Dimensions(path);
        if (decode)
        {
#if AVALONIA
            using var decoded = PackageImages.Load(path);
#else
            PackageImages.Load(path);
#endif
        }
        return dimensions;
    }
}

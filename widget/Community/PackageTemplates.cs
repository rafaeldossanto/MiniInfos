using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MiniInfos.Mascot;

namespace MiniInfos.Community;

public sealed record PackageDraft(PackageManifest Manifest, IReadOnlyDictionary<string, byte[]> Assets);

/// <summary>A mascot's PNG grid with the track each row plays.</summary>
public sealed record MascotSheet(BitmapSource Image, int Columns, IReadOnlyDictionary<PetState, MascotTrack> Tracks, PetAnatomy? Anatomy = null);

/// <summary>Produces editable, importable examples from the actual rendering contract.</summary>
public static class PackageTemplates
{
    public static PackageDraft Theme(ResourceDictionary resources)
    {
        var colors = ThemeDefinition.ColorKeys.ToDictionary(key => key, key => ((SolidColorBrush)resources[key]).Color.ToString());
        var assets = new Dictionary<string, byte[]> { ["README.md"] = Instructions(PackageKind.Theme) };
        string? header = null;
        if (resources["HeaderArtworkVisibility"] is Visibility.Visible && resources["HeaderArtwork"] is BitmapSource image)
        {
            header = "header.png";
            assets[header] = PackageImages.Encode(image);
        }
        var definition = new ThemeDefinition(Colors: colors, HeaderImage: header,
            HeaderHeight: (double)resources["HeaderHeight"], HeaderTitleSize: (double)resources["HeaderTitleSize"]);
        return new(new(1, PackageKind.Theme, "community.meu-tema", L.Text("Meu tema"), L.Text("Seu nome"), "1.0.0", "UNLICENSED",
            L.Text("Edite o manifest.json e compartilhe seu tema."), Theme: definition), assets);
    }

    /// <summary>A built-in mascot laid out as a package's grid: a row per state, in the enum's order, and a column per pose.</summary>
    public static async Task<MascotSheet> MascotSheetAsync(IPetAppearance? appearance = null)
    {
        appearance ??= PetAppearances.Athena;
        var states = Enum.GetValues<PetState>();
        var side = PetFrame.CanvasSize;
        var bitmap = new WriteableBitmap(side * appearance.FrameCount, side * states.Length, 96, 96, PixelFormats.Pbgra32, null);
        var tracks = new Dictionary<PetState, MascotTrack>();
        for (var row = 0; row < states.Length; row++)
        {
            for (var column = 0; column < appearance.FrameCount; column++)
            {
                var frame = appearance.Frame(states[row], column);
                bitmap.WritePixels(new Int32Rect(column * side, row * side, side, side), frame.Pixels.ToArray(), side * 4, 0);
            }
            tracks[states[row]] = new(row, DurationsMs: PetAnimation.DurationsFor(states[row]).ToArray(),
                BlendMs: states[row] == PetState.Idle ? 160 : 0);
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        bitmap.Freeze();
        return new(bitmap, appearance.FrameCount, tracks, appearance.Anatomy);
    }

    /// <param name="sheet">A grid already laid out, as the example window has; built here when left out.</param>
    public static async Task<PackageDraft> MascotAsync(MascotSheet? sheet = null, IPetAppearance? appearance = null)
    {
        sheet ??= await MascotSheetAsync(appearance);
        var bytes = await Task.Run(() => PackageImages.Encode(sheet.Image));
        return new(MascotManifest(sheet),
            new Dictionary<string, byte[]> { ["sprites.png"] = bytes, ["README.md"] = Instructions(PackageKind.Mascot) });
    }

    /// <summary>The manifest the mascot model ships with: every state's track and the selected character's anatomy.</summary>
    public static PackageManifest MascotManifest(MascotSheet sheet) => new(1, PackageKind.Mascot, "community.meu-mascote", L.Text("Meu mascote"),
        L.Text("Seu nome"), "1.0.0", "UNLICENSED", L.Text("Substitua as ilustrações, ajuste a anatomia e edite sua identificação antes de publicar."),
        Mascot: new("sprites.png", sheet.Columns, sheet.Tracks.Count, new(sheet.Tracks), sheet.Anatomy ?? PetAnatomy.Athena));

    private static byte[] Instructions(PackageKind kind) => Encoding.UTF8.GetBytes(L.Text("# Meu pacote do MiniInfos\n\nEste arquivo .miniinfos é um ZIP. Faça uma cópia, renomeie para .zip e extraia.\nEdite manifest.json: use um ID próprio (autor.nome), nome, autor, versão e licença.\nUNLICENSED é apenas um marcador do modelo: preencha a licença antes de compartilhar.\nMantenha schemaVersion 1 e manifest.json na raiz do ZIP ao compactar novamente.\nPara testar, use Adicionar tema ou Adicionar mascote nas configurações do MiniInfos.\nOs nomes das propriedades do manifesto são o contrato do pacote; campos desconhecidos são rejeitados.") + (kind == PackageKind.Theme ? L.Text("\nAs cores ficam em theme.colors, no formato #RRGGBB ou #AARRGGBB.\nO tema base pode ser Simple, Light ou Classic. headerImage é um PNG opcional.") : L.Text("\nsprites.png é uma grade de células iguais, com fundo transparente.\nmascot.columns e mascot.rows descrevem a grade; states associa cada animação a uma linha.\nIdle é obrigatória. Frames e durationsMs permitem escolher os quadros e suas durações.\nA anatomia usa coordenadas de 0 a 320, independentemente do tamanho da imagem.\nAjuste regiões clicáveis, pontos das poses e centros de massa para sua personagem.\nA física de arraste e rotação é fornecida pelo MiniInfos.")));
}

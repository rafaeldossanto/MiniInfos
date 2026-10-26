using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MiniInfos.Mascot;

namespace MiniInfos.Community;

public sealed record PackageDraft(PackageManifest Manifest, IReadOnlyDictionary<string, byte[]> Assets);

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
            assets[header] = Encode(image);
        }
        var definition = new ThemeDefinition(Colors: colors, HeaderImage: header,
            HeaderHeight: (double)resources["HeaderHeight"], HeaderTitleSize: (double)resources["HeaderTitleSize"]);
        return new(new(1, PackageKind.Theme, "community.meu-tema", "Meu tema", "Seu nome", "1.0.0", "UNLICENSED",
            "Edite o manifest.json e compartilhe seu tema.", Theme: definition), assets);
    }

    public static async Task<PackageDraft> MascotAsync()
    {
        var states = Enum.GetValues<PetState>();
        var side = PetFrame.CanvasSize;
        var bitmap = new WriteableBitmap(side * PetSprites.FrameCount, side * states.Length, 96, 96, PixelFormats.Pbgra32, null);
        var tracks = new Dictionary<PetState, MascotTrack>();
        for (var row = 0; row < states.Length; row++)
        {
            for (var column = 0; column < PetSprites.FrameCount; column++)
            {
                var frame = PetSprites.Frame(states[row], column);
                bitmap.WritePixels(new Int32Rect(column * side, row * side, side, side), frame.Pixels.ToArray(), side * 4, 0);
            }
            tracks[states[row]] = new(row, DurationsMs: PetAnimation.DurationsFor(states[row]).ToArray(),
                BlendMs: states[row] == PetState.Idle ? 160 : 0);
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        bitmap.Freeze();
        var bytes = await Task.Run(() => Encode(bitmap));
        return new(new(1, PackageKind.Mascot, "community.meu-mascote", "Meu mascote", "Seu nome", "1.0.0", "UNLICENSED",
            "Substitua as ilustrações, ajuste a anatomia e edite sua identificação antes de publicar.",
            Mascot: new("sprites.png", PetSprites.FrameCount, states.Length, tracks, PetAnatomy.Athena)),
            new Dictionary<string, byte[]> { ["sprites.png"] = bytes, ["README.md"] = Instructions(PackageKind.Mascot) });
    }

    private static byte[] Encode(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static byte[] Instructions(PackageKind kind) => Encoding.UTF8.GetBytes("""
        # Meu pacote do MiniInfos

        Este arquivo .miniinfos é um ZIP. Faça uma cópia, renomeie para .zip e extraia.
        Edite manifest.json: use um ID próprio (autor.nome), nome, autor, versão e licença.
        UNLICENSED é apenas um marcador do modelo: preencha a licença antes de compartilhar.
        Mantenha schemaVersion 1 e manifest.json na raiz do ZIP ao compactar novamente.
        Para testar, use Adicionar tema ou Adicionar mascote nas configurações do MiniInfos.
        Os nomes das propriedades do manifesto são o contrato do pacote; campos desconhecidos são rejeitados.
        """ + (kind == PackageKind.Theme ? """

        As cores ficam em theme.colors, no formato #RRGGBB ou #AARRGGBB.
        O tema base pode ser Simple, Light ou Classic. headerImage é um PNG opcional.
        """ : """

        sprites.png é uma grade de células iguais, com fundo transparente.
        mascot.columns e mascot.rows descrevem a grade; states associa cada animação a uma linha.
        Idle é obrigatória. Frames e durationsMs permitem escolher os quadros e suas durações.
        A anatomia usa coordenadas de 0 a 320, independentemente do tamanho da imagem.
        Ajuste regiões clicáveis, pontos das poses e centros de massa para sua personagem.
        A física de arraste e rotação é fornecida pelo MiniInfos.
        """));
}

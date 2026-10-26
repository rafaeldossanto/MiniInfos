using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MiniInfos.Mascot;
using MiniInfos.UI.Themes;

namespace MiniInfos.Community;

/// <summary>Small, detached artwork shared by the installed library and catalog tools.</summary>
public static class PackagePreviews
{
    public const int Size = 128;

    public static BitmapSource Mascot(InstalledPackage package)
    {
        var definition = package.Manifest.Mascot ?? throw new PackageException("O pacote não contém um mascote.");
        var sheet = PackageImages.Load(PackageFiles.Resolve(package.Directory, definition.SpriteSheet));
        var idle = definition.States[PetState.Idle];
        var width = sheet.PixelWidth / definition.Columns;
        var height = sheet.PixelHeight / definition.Rows;
        var cell = new CroppedBitmap(sheet, new Int32Rect((idle.Frames?[0] ?? 0) * width, idle.Row * height, width, height));
        cell.Freeze();
        return Mascot(new PetFrame(cell, new Rect(0, 0, PetFrame.CanvasSize, PetFrame.CanvasSize)));
    }

    public static BitmapSource Mascot(PetFrame frame)
    {
        var bounds = PetPixels.Bounds(frame.Pixels.Span, PetFrame.CanvasSize);
        var scale = Math.Min((Size - 16) / bounds.Width, (Size - 16) / bounds.Height);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.PushTransform(new MatrixTransform(scale, 0, 0, scale,
                (Size - bounds.Width * scale) / 2 - bounds.X * scale,
                (Size - bounds.Height * scale) / 2 - bounds.Y * scale));
            drawing.DrawImage(frame.Source, new Rect(0, 0, PetFrame.CanvasSize, PetFrame.CanvasSize));
            drawing.Pop();
        }
        return Render(visual);
    }

    public static BitmapSource Theme(InstalledPackage package)
    {
        // Initialize WPF's embedded-resource resolver in console hosts, without creating an Application/window.
        _ = Application.ResourceAssembly;
        return Theme(CommunityTheme.Load(package));
    }

    /// <summary>A miniature card in a theme's brushes, built-in or community: header, two bars and the footer.</summary>
    public static BitmapSource Theme(ResourceDictionary resources)
    {
        Brush Color(string key) => (Brush)resources[key];
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRoundedRectangle(Color("CardBackground"), new Pen(Color("CardBorder"), 2), new Rect(2, 2, 124, 124), 8, 8);
            if (resources["HeaderArtworkVisibility"] is Visibility.Visible && resources["HeaderArtwork"] is ImageSource header)
            {
                drawing.PushClip(new RectangleGeometry(new Rect(4, 4, 120, 38), 6, 6));
                drawing.DrawImage(header, new Rect(4, 4, 120, 38));
                drawing.Pop();
            }
            else drawing.DrawRoundedRectangle(Color("PrimaryText"), null, new Rect(14, 16, 48, 5), 2, 2);
            for (var row = 0; row < 2; row++)
            {
                var y = 51 + row * 29;
                drawing.DrawRoundedRectangle(Color("DimText"), null, new Rect(14, y, 27, 3), 1, 1);
                drawing.DrawRoundedRectangle(Color("Track"), null, new Rect(14, y + 9, 100, 6), 3, 3);
                drawing.DrawRoundedRectangle(Color("Accent"), null, new Rect(14, y + 9, row == 0 ? 63 : 38, 6), 3, 3);
            }
            drawing.DrawRectangle(Color("Divider"), null, new Rect(14, 105, 100, 1));
            drawing.DrawRoundedRectangle(Color("PrimaryText"), null, new Rect(14, 114, 32, 4), 1, 1);
            drawing.DrawRoundedRectangle(Color("PrimaryText"), null, new Rect(76, 114, 32, 4), 1, 1);
        }
        return Render(visual);
    }

    private static BitmapSource Render(DrawingVisual visual)
    {
        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}

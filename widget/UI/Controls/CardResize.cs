using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MiniInfos.Infrastructure.Windows;

namespace MiniInfos.UI.Controls;

/// <summary>Every card keeps its design proportions while a Windows resize edge is dragged.</summary>
public sealed class CardResize
{
    public const double MinScale = 0.75;
    public const double MaxScale = 3;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;
    private readonly Window _window;
    private readonly Func<Size> _designSize;
    private readonly Action _save;
    private HwndSource? _source;

    public CardResize(Window window, Func<Size> designSize, Action save)
    {
        _window = window;
        _designSize = designSize;
        _save = save;
        window.SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            _source.AddHook(OnMessage);
        };
        window.Closed += (_, _) => _source?.RemoveHook(OnMessage);
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmExitSizeMove) _save();
        var design = _designSize();
        if (message != WmSizing || design.Height <= 0) return IntPtr.Zero;

        var dpi = VisualTreeHelper.GetDpi(_window).DpiScaleX;
        var fitted = AspectSizing.Fit(Marshal.PtrToStructure<PixelRect>(lParam), wParam.ToInt32(),
            design.Width / design.Height,
            (int)Math.Round(design.Width * MinScale * dpi),
            (int)Math.Round(design.Width * MaxScale * dpi));
        Marshal.StructureToPtr(fitted, lParam, fDeleteOld: false);
        handled = true;
        return 1;
    }
}

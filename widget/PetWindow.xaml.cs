using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MiniInfos;

/// <summary>A transparent desktop companion with no owner: hiding a card leaves the pet reachable.</summary>
public partial class PetWindow : Window
{
    private readonly Preferences _preferences;
    private readonly PetActivity _activity;
    private readonly Action _showCards;
    private readonly Action _openSettings;
    private readonly Action _quit;
    private readonly JsonFile<PetPlacement> _placement = new(Path.Combine(AppPaths.Data, "mascot.json"));
    private readonly PetAnimationPlayer _player = new((state, index) => PetSprites.Frame(state, index).Pixels,
        PetFrame.CanvasSize * PetFrame.CanvasSize * 4);
    private readonly WriteableBitmap _surface = new(PetFrame.CanvasSize, PetFrame.CanvasSize, 96, 96, PixelFormats.Pbgra32, null);
    private readonly MatrixTransform _motion = new();
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private long _lastRenderFrame = -1;
    private PetState? _state;

    public PetWindow(Preferences preferences, PetActivity activity, Action showCards, Action openSettings, Action quit)
    {
        _preferences = preferences;
        _activity = activity;
        _showCards = showCards;
        _openSettings = openSettings;
        _quit = quit;
        InitializeComponent();
        Sprite.Source = _surface;
        Sprite.RenderTransform = _motion;
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });
        Width = Height = PetPlacement.Size(preferences.PetSize);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x80); // WS_EX_TOOLWINDOW: no Alt+Tab entry.
            HwndSource.FromHwnd(handle).AddHook(OnMessage);
        };
        Loaded += (_, _) =>
        {
            RestorePosition(_placement.Load());
            Paint(Stopwatch.GetElapsedTime(_startedAt));
            CompositionTarget.Rendering += OnRendering;
        };
        Closed += (_, _) => { CompositionTarget.Rendering -= OnRendering; SavePosition(); };
        Paint(Stopwatch.GetElapsedTime(_startedAt));
    }

    public PetState State => _activity.StateAt(DateTimeOffset.UtcNow);

    public void ResizePet()
    {
        var center = Left + Width / 2;
        var bottom = Top + Height;
        Width = Height = PetPlacement.Size(_preferences.PetSize);
        if (!IsLoaded) return;
        Left = center - Width / 2;
        Top = bottom - Height;
        KeepOnScreen();
        SavePosition();
    }

    public void BackToCorner() { RestorePosition(null); SavePosition(); }

    private void OnRendering(object? sender, EventArgs e)
    {
        // Match display rendering, capped at 60 updates/s on high-refresh monitors.
        var frame = (long)(((RenderingEventArgs)e).RenderingTime.TotalSeconds * 60);
        if (frame == _lastRenderFrame) return;
        _lastRenderFrame = frame;
        Paint(Stopwatch.GetElapsedTime(_startedAt));
    }

    private void Paint(TimeSpan elapsed)
    {
        var state = State;
        if (_player.Draw(state, elapsed))
            _surface.WritePixels(new Int32Rect(0, 0, PetFrame.CanvasSize, PetFrame.CanvasSize),
                _player.Buffer, PetFrame.CanvasSize * 4, 0);

        var pose = PetAnimation.MotionAt(elapsed);
        var matrix = Matrix.Identity;
        matrix.ScaleAt(pose.ScaleX, pose.ScaleY, Width / 2, Height * 0.94);
        matrix.RotateAt(pose.Angle, Width / 2, Height * 0.94);
        matrix.Translate(pose.X * Width, pose.Y * Height);
        _motion.Matrix = matrix;
        if (state == _state) return;
        _state = state;
        var status = state switch
        {
            PetState.Active => "Tokens aumentaram recentemente",
            PetState.Alert => "Uma janela de uso está em 90% ou mais",
            PetState.Sleep => "Sem novos tokens há alguns minutos",
            _ => "De olho nos seus tokens",
        };
        Sprite.ToolTip = $"Coruja de Atena · {status}\nArraste para mover · clique duplo para mostrar o MiniInfos\nAlt + roda do mouse muda o tamanho";
    }

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _showCards();
        }
        else
        {
            DragMove();
            KeepOnScreen();
            SavePosition();
        }
        e.Handled = true;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        _preferences.PetSize = (PetSizeChoice)Math.Clamp((int)_preferences.PetSize + Math.Sign(e.Delta), 0, 2);
        e.Handled = true;
    }

    private void RestorePosition(PetPlacement? saved)
    {
        var work = SystemParameters.WorkArea;
        var position = saved ?? new PetPlacement(work.Right - Width - 24, work.Bottom - Height - 24);
        Left = position.Left; Top = position.Top;
        KeepOnScreen();
    }

    private void KeepOnScreen()
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var position = new PetPlacement(Left, Top).KeepVisible(screen, Width);
        Left = position.Left; Top = position.Top;
    }

    private void SavePosition()
    {
        if (IsLoaded && double.IsFinite(Left) && double.IsFinite(Top)) _placement.Save(new PetPlacement(Left, Top));
    }

    private nint OnMessage(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021) // WM_MOUSEACTIVATE: keep typing in the foreground app.
        {
            handled = true;
            return 3; // MA_NOACTIVATE
        }
        if (message != 0x0084 || _state is null) return 0; // WM_NCHITTEST
        var value = lParam.ToInt64();
        var local = PointFromScreen(new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF)));
        var inverse = _motion.Matrix;
        inverse.Invert();
        local = inverse.Transform(local);
        var x = local.X / Width * PetFrame.CanvasSize;
        var y = local.Y / Height * PetFrame.CanvasSize;
        if (x >= 0 && x < PetFrame.CanvasSize && y >= 0 && y < PetFrame.CanvasSize
            && _player.Pixels.Span[((int)y * PetFrame.CanvasSize + (int)x) * 4 + 3] >= 32) return 0;
        handled = true;
        return -1; // HTTRANSPARENT: desktop clicks pass through the transparent padding.
    }

    private void OnShowCards(object sender, RoutedEventArgs e) => _showCards();
    private void OnSettings(object sender, RoutedEventArgs e) => _openSettings();
    private void OnBackToCorner(object sender, RoutedEventArgs e) => BackToCorner();
    private void OnHidePet(object sender, RoutedEventArgs e) => _preferences.PetEnabled = false;
    private void OnQuit(object sender, RoutedEventArgs e) => _quit();

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(nint window, int index, int value);
}

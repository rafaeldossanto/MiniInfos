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
    private readonly PetInteraction _interaction = new();
    private Matrix _grabMatrix;
    private Matrix _releaseMatrix;
    private TimeSpan? _releasedAt;
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private long _lastRenderFrame = -1;
    private PetState? _state;
    private bool _wasReaction;

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

    private TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startedAt);
    public PetState State => _interaction.StateAt(Elapsed) ?? _activity.StateAt(DateTimeOffset.UtcNow);

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
        if (_interaction.IsPressed && IsMouseCaptured) _interaction.Move(CursorScreen(), elapsed);
        var hold = _interaction.HoldAt(elapsed);
        var reaction = hold?.State ?? _interaction.StateAt(elapsed);
        var state = reaction ?? _activity.StateAt(DateTimeOffset.UtcNow);
        if (_player.Draw(state, elapsed, _interaction.AnimationRevision))
            _surface.WritePixels(new Int32Rect(0, 0, PetFrame.CanvasSize, PetFrame.CanvasSize),
                _player.Buffer, PetFrame.CanvasSize * 4, 0);

        var matrix = Matrix.Identity;
        if (hold is { } grip)
        {
            var anchor = ToWindow(grip.Anchor);
            matrix.ScaleAt(state == PetState.HeldBody ? 1.02 : 0.95, state == PetState.HeldBody ? 0.94 : 0.95, anchor.X, anchor.Y);
            matrix.RotateAt(grip.Angle, anchor.X, anchor.Y);
            matrix = Blend(_grabMatrix, matrix, grip.Progress);
            if (IsLoaded)
            {
                var pointer = PointFromScreen(_interaction.PointerScreen);
                var attached = matrix.Transform(anchor);
                Left += pointer.X - attached.X;
                Top += pointer.Y - attached.Y;
            }
        }
        else
        {
            var pose = PetAnimation.MotionAt(elapsed);
            matrix.ScaleAt(pose.ScaleX, pose.ScaleY, Width / 2, Height * 0.94);
            matrix.RotateAt(pose.Angle, Width / 2, Height * 0.94);
            matrix.Translate(pose.X * Width, pose.Y * Height);
            if (_releasedAt is { } released)
            {
                var progress = (elapsed - released) / PetAnimation.StateTransition;
                matrix = Blend(_releaseMatrix, matrix, PetAnimation.Ease(progress));
                if (progress >= 1) _releasedAt = null;
            }
        }
        _motion.Matrix = matrix;
        Sprite.Cursor = hold is null ? Cursors.Hand : Cursors.SizeAll;
        if (state == _state && _wasReaction == (reaction is not null)) return;
        _state = state;
        _wasReaction = reaction is not null;
        var status = state switch
        {
            PetState.Active when reaction is not null => "Brincando com você",
            PetState.Active => "Tokens aumentaram recentemente",
            PetState.Alert => "Uma janela de uso está em 90% ou mais",
            PetState.Sleep => "Sem novos tokens há alguns minutos",
            PetState.ProtectEyes => "Ei! Protegendo os olhinhos",
            PetState.HeldHead => "Pendurada pela cabeça",
            PetState.HeldLeftWing or PetState.HeldRightWing => "Pendurada pela asa",
            PetState.HeldFeet => "De cabeça para baixo!",
            PetState.HeldBody => "Segurando pela barriguinha",
            _ => "De olho nos seus tokens",
        };
        Sprite.ToolTip = $"Coruja de Atena · {status}\nToque nos olhos · segure pela cabeça, asas ou pés para mover\nClique duplo para mostrar o MiniInfos · Alt + roda muda o tamanho";
    }

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _interaction.CancelPress();
            if (IsMouseCaptured) ReleaseMouseCapture();
            _showCards();
        }
        else
        {
            var local = e.GetPosition(this);
            var unposed = _motion.Matrix;
            unposed.Invert();
            var canvas = unposed.Transform(local);
            canvas = new Point(canvas.X / Width * PetFrame.CanvasSize, canvas.Y / Height * PetFrame.CanvasSize);
            _grabMatrix = _motion.Matrix;
            _releasedAt = null;
            _interaction.Press(canvas, PointToScreen(local), Elapsed);
            if (!CaptureMouse()) _interaction.CancelPress();
        }
        e.Handled = true;
    }

    private void OnMoved(object sender, MouseEventArgs e)
    {
        if (!_interaction.IsPressed) return;
        // Global cursor coordinates remain stable while the captured window moves underneath it.
        _interaction.Move(CursorScreen(), Elapsed);
        e.Handled = true;
    }

    private void OnReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_interaction.IsPressed) return;
        var elapsed = Elapsed;
        if (IsMouseCaptured)
        {
            _interaction.Move(CursorScreen(), elapsed);
            Paint(elapsed);
        }
        if (_interaction.Release(elapsed)) FinishDrop(elapsed);
        if (IsMouseCaptured) ReleaseMouseCapture();
        Paint(elapsed);
        e.Handled = true;
    }

    private void OnCaptureLost(object sender, MouseEventArgs e)
    {
        if (!_interaction.IsPressed) return;
        var wasHeld = _interaction.IsHeld;
        _interaction.CancelPress();
        if (wasHeld) FinishDrop(Elapsed);
    }

    private void FinishDrop(TimeSpan now)
    {
        _releaseMatrix = _motion.Matrix;
        _releasedAt = now;
        KeepOnScreen();
        SavePosition();
    }

    private Point ToWindow(Point canvas) => new(canvas.X / PetFrame.CanvasSize * Width, canvas.Y / PetFrame.CanvasSize * Height);

    private Point CursorScreen() => GetCursorPos(out var point) ? new Point(point.X, point.Y) : PointToScreen(Mouse.GetPosition(this));

    private static Matrix Blend(Matrix from, Matrix to, double amount)
    {
        double Lerp(double a, double b) => a + (b - a) * amount;
        return new Matrix(Lerp(from.M11, to.M11), Lerp(from.M12, to.M12), Lerp(from.M21, to.M21), Lerp(from.M22, to.M22),
            Lerp(from.OffsetX, to.OffsetX), Lerp(from.OffsetY, to.OffsetY));
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (_interaction.IsPressed) return;
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
        if (_interaction.IsPressed) { handled = true; return 1; } // Keep the drag captured, even outside the silhouette.
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

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X; public int Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out CursorPoint point);
}

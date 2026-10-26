using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Mascot;
using MiniInfos.Settings;

namespace MiniInfos.UI.Windows;

/// <summary>A transparent desktop companion with no owner: hiding a card leaves the pet reachable.</summary>
public partial class PetWindow : Window
{
    private readonly Preferences _preferences;
    private readonly PetActivity _activity;
    private readonly Action _showCards;
    private readonly Action _openSettings;
    private readonly Action _quit;
    private readonly JsonFile<PetPlacement> _placement = new(Path.Combine(AppPaths.Data, "mascot.json"));
    private readonly PetAnimationPlayer _player;
    private readonly IPetAppearance _appearance;
    private readonly WriteableBitmap _surface = new(PetFrame.CanvasSize, PetFrame.CanvasSize, 96, 96, PixelFormats.Pbgra32, null);
    private readonly MatrixTransform _motion = new();
    private readonly PetInteraction _interaction;
    private Matrix _grabMatrix;
    private Matrix _releaseMatrix;
    private Point _releaseOrigin;
    private PetPlacement? _dropPlacement;
    private TimeSpan? _releasedAt;
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private readonly PetFrameClock _frameClock = new();
    private PetState? _state;
    private bool _wasReaction;

    public PetWindow(Preferences preferences, PetActivity activity, Action showCards, Action openSettings, Action quit)
    {
        _preferences = preferences;
        _activity = activity;
        _showCards = showCards;
        _openSettings = openSettings;
        _quit = quit;
        _appearance = PetAppearances.Load(preferences.PetId);
        _interaction = new(_appearance.Anatomy, _appearance.HasPose);
        _player = new((state, index) => _appearance.Frame(state, index).Pixels,
            PetFrame.CanvasSize * PetFrame.CanvasSize * 4, _appearance.PoseAt);
        InitializeComponent();
        Title = _appearance.Name + " — MiniInfos";
        Sprite.Source = _surface;
        Sprite.RenderTransform = _motion;
        SetBinding(TopmostProperty, new Binding(nameof(Preferences.IsTopmost)) { Source = preferences });
        Width = Height = Sprite.Width = Sprite.Height = PetPlacement.Size(preferences.PetSize);
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
    private double SpriteSize => Sprite.Width;
    public PetState State => _interaction.StateAt(Elapsed) ?? _activity.StateAt(DateTimeOffset.UtcNow);

    public void ResizePet()
    {
        var position = CurrentPlacement();
        var center = position.Left + SpriteSize / 2;
        var bottom = position.Top + SpriteSize;
        _interaction.CancelPress();
        if (IsMouseCaptured) ReleaseMouseCapture();
        _releasedAt = null;
        _dropPlacement = null;
        Width = Height = Sprite.Width = Sprite.Height = PetPlacement.Size(_preferences.PetSize);
        if (!IsLoaded) return;
        Left = center - Width / 2;
        Top = bottom - Height;
        KeepOnScreen();
        Paint(Elapsed);
        SavePosition();
    }

    public void BackToCorner()
    {
        _interaction.CancelPress();
        if (IsMouseCaptured) ReleaseMouseCapture();
        _releasedAt = null;
        _dropPlacement = null;
        Width = Height = SpriteSize;
        RestorePosition(null);
        Paint(Elapsed);
        SavePosition();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_frameClock.Tick(((RenderingEventArgs)e).RenderingTime, _interaction.IsPressed || _releasedAt is not null)) return;
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
            matrix = PetGeometry.Blend(_grabMatrix, matrix, grip.Progress, grip.Angle);
        }
        else
        {
            var pose = PetAnimation.MotionAt(elapsed);
            matrix.ScaleAt(pose.ScaleX, pose.ScaleY, SpriteSize / 2, SpriteSize * 0.94);
            matrix.RotateAt(pose.Angle, SpriteSize / 2, SpriteSize * 0.94);
            matrix.Translate(pose.X * SpriteSize, pose.Y * SpriteSize);
            if (_releasedAt is { } released)
            {
                var progress = (elapsed - released) / PetAnimation.StateTransition;
                matrix = PetGeometry.Blend(_releaseMatrix, matrix, PetAnimation.Ease(progress));
            }
        }
        var returning = _releasedAt is not null;
        var settled = returning && elapsed - _releasedAt!.Value >= PetAnimation.StateTransition;
        var viewport = hold is { } holding
            ? PetGeometry.Around(matrix, SpriteSize, ToWindow(holding.Anchor))
            : returning && !settled
                ? PetGeometry.Around(matrix, SpriteSize, ToWindow(_appearance.RestCenter))
                : PetGeometry.Fit(matrix, SpriteSize, rotating: false);
        matrix = viewport.Transform;
        if (Width != viewport.Bounds.Width) Width = viewport.Bounds.Width;
        if (Height != viewport.Bounds.Height) Height = viewport.Bounds.Height;
        if (IsLoaded && hold is { } attachedGrip)
        {
            var pointer = PointFromScreen(_interaction.PointerScreen);
            var attached = matrix.Transform(ToWindow(attachedGrip.Anchor));
            Left += pointer.X - attached.X;
            Top += pointer.Y - attached.Y;
            // HWND positions round to whole screen pixels; keep the residual in the sprite transform.
            var residual = PointFromScreen(_interaction.PointerScreen) - matrix.Transform(ToWindow(attachedGrip.Anchor));
            matrix.Translate(residual.X, residual.Y);
        }
        else if (IsLoaded && returning)
        {
            var origin = PointFromScreen(_releaseOrigin);
            Left += origin.X + viewport.Bounds.X;
            Top += origin.Y + viewport.Bounds.Y;
            var residual = PointFromScreen(_releaseOrigin) + (Vector)viewport.Bounds.TopLeft;
            matrix.Translate(residual.X, residual.Y);
            if (settled)
            {
                _releasedAt = null;
                _dropPlacement = null;
                // The final rest transform only moves transparent padding beyond the original canvas.
                Width = Height = SpriteSize;
                SavePosition();
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
        Sprite.ToolTip = $"{_appearance.Name} · {status}\nToque nos olhos · segure para mover e girar\nClique duplo para mostrar o MiniInfos · Alt + roda muda o tamanho";
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
            canvas = new Point(canvas.X / SpriteSize * PetFrame.CanvasSize, canvas.Y / SpriteSize * PetFrame.CanvasSize);
            _grabMatrix = _motion.Matrix;
            _releasedAt = null;
            _dropPlacement = null;
            _interaction.Press(canvas, PointToScreen(local), Elapsed, SpriteSize / PetFrame.CanvasSize * VisualTreeHelper.GetDpi(this).DpiScaleX);
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
        var target = CurrentPlacement();
        var offset = new Vector(target.Left - Left, target.Top - Top);
        _releaseMatrix = _motion.Matrix;
        _releaseMatrix.Translate(-offset.X, -offset.Y);
        _releaseOrigin = PointToScreen(new Point(offset.X, offset.Y));
        _dropPlacement = target;
        _releasedAt = now;
        SavePosition();
    }

    private Point ToWindow(Point canvas) => new(canvas.X / PetFrame.CanvasSize * SpriteSize, canvas.Y / PetFrame.CanvasSize * SpriteSize);

    private Point CursorScreen() => GetCursorPos(out var point) ? new Point(point.X, point.Y) : PointToScreen(Mouse.GetPosition(this));

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
        var position = saved ?? new PetPlacement(work.Right - SpriteSize - 24, work.Bottom - SpriteSize - 24);
        Left = position.Left; Top = position.Top;
        KeepOnScreen();
    }

    private void KeepOnScreen()
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var position = new PetPlacement(Left, Top).KeepVisible(screen, SpriteSize);
        Left = position.Left; Top = position.Top;
    }

    private void SavePosition()
    {
        if (IsLoaded && double.IsFinite(Left) && double.IsFinite(Top)) _placement.Save(CurrentPlacement());
    }

    private PetPlacement CurrentPlacement()
    {
        if (_dropPlacement is { } drop) return drop;
        if (Width == SpriteSize && Height == SpriteSize && !_interaction.IsHeld) return new(Left, Top);
        // Return upright around the body's visible center, independent of the temporary rotating viewport.
        var center = ToWindow(_appearance.RestCenter);
        var offset = _motion.Matrix.Transform(center) - center;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        return new PetPlacement(Left + offset.X, Top + offset.Y).KeepVisible(screen, SpriteSize);
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
        var x = local.X / SpriteSize * PetFrame.CanvasSize;
        var y = local.Y / SpriteSize * PetFrame.CanvasSize;
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

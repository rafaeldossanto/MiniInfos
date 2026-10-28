using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MiniInfos.Infrastructure.Storage;
using MiniInfos.Mascot;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static MiniInfos.Desktop.UI.Elements;

namespace MiniInfos.Desktop.UI;

internal sealed class PetWindow : Window
{
    private readonly DesktopSession _session;
    private readonly PetActivity _activity;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly IPetAppearance _appearance;
    private readonly PetInteraction _interaction;
    private readonly PetAnimationPlayer _player;
    private readonly WriteableBitmap _bitmap = new(new PixelSize(320, 320), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    private readonly Image _image = new() { Width = 320, Height = 320 };
    private readonly Canvas _canvas = new() { ClipToBounds = false };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 120) };
    private readonly JsonFile<PetPlacement> _placement = new(Path.Combine(AppPaths.Data, "pet-window.json"));
    private PetHold? _held;
    private PetState _state;
    private PetDrop? _drop;
    private TimeSpan _released;
    private Point _dropCenter;
    private PixelPoint _dropOrigin;
    private double _lastAngle;
    private Vector _lastScale = new(1, 1);
    private Point _lastCenter;
    private PixelPoint _settle;
    private PixelPoint _origin;
    private Matrix _lastMatrix = Matrix.Identity;
    private double SpriteSize => PetPlacement.Size(_session.Preferences.PetSize);
    private double Scale => SpriteSize / 320;
    // Extra transparent canvas leaves room for momentum and short drops without clipping the mascot.
    private const double MarginCanvas = 160;
    public string? AppearanceId { get; }
    public PetWindow(DesktopSession session, PetActivity activity)
    {
        _session = session; _activity = activity; AppearanceId = session.Preferences.PetId;
        _appearance = PetAppearances.Load(AppearanceId);
        _interaction = new PetInteraction(_appearance.Anatomy, _appearance.HasPose);
        _player = new PetAnimationPlayer((state, index) => _appearance.Frame(state, index).Pixels, 320 * 320 * 4, _appearance.PoseAt);
        Title = _appearance.Name; CanResize = false; WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true; ShowInTaskbar = false;
        _image.Source = _bitmap; _canvas.Children.Add(_image); Content = _canvas;
        _image.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        SetSize();
        var saved = _placement.Load() ?? new PetPlacement(120, 180);
        _origin = Position = new PixelPoint((int)saved.Left, (int)saved.Top);
        _image.PointerPressed += Press; _image.PointerMoved += Move; _image.PointerReleased += Release;
        _image.PointerCaptureLost += (_, _) => { if (_interaction.IsPressed) ReleaseHold(); };
        _image.ContextMenu = new ContextMenu { Items = { Menu("Mostrar MiniInfos", session.ShowCards), Menu("Configurações", session.OpenSettings), Menu("Esconder mascote", () => session.Preferences.PetEnabled = false), Menu("Sair do MiniInfos", session.Quit) } };
        _timer.Tick += Draw;
        Opened += (_, _) =>
        {
            var area = Screens.ScreenFromWindow(this)?.WorkingArea;
            if (area is { } screen && !screen.Contains(Position)) _origin = new PixelPoint(screen.X + 50, screen.Y + 50);
            _timer.Start();
        };
        Closed += (_, _) => { _timer.Stop(); Save(); _bitmap.Dispose(); };
    }
    public void SetSize() { Width = Height = SpriteSize + 2 * MarginCanvas; }
    private Point Screen(PointerEventArgs e) { var point = this.PointToScreen(e.GetPosition(this)); return new Point(point.X, point.Y); }
    private Point CanvasPoint(PointerEventArgs e) => e.GetPosition(_image);
    private void Press(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !_appearance.Frame(_state, 0).Hits(CanvasPoint(e))) return;
        _drop = null; _interaction.Press(CanvasPoint(e), Screen(e), _clock.Elapsed, Scale * RenderScaling);
        e.Pointer.Capture(_image); e.Handled = true;
    }
    private void Move(object? sender, PointerEventArgs e) { if (_interaction.IsPressed) _interaction.Move(Screen(e), _clock.Elapsed); }
    private void Release(object? sender, PointerReleasedEventArgs e) { ReleaseHold(); e.Pointer.Capture(null); e.Handled = true; }
    private void ReleaseHold()
    {
        var now = _clock.Elapsed;
        if (!_interaction.Release(now)) return;
        if (_state == PetState.HeldFeet)
        {
            _player.HalfTurn(); _lastAngle += 180;
            var canonical = Matrix.CreateTranslation(-160, -160) * Matrix.CreateRotation(Math.PI) * Matrix.CreateTranslation(160, 160) * _lastMatrix;
            _lastCenter = canonical.Transform(_appearance.RestCenter);
        }
        var bounds = PetPixels.Bounds(_player.Pixels.Span, 320);
        var rest = PetPixels.Bounds(_appearance.Frame(PetState.Idle, 0).Pixels.Span, 320);
        var center = _appearance.RestCenter;
        var sourceBounds = new Rect((bounds.X - center.X) * Scale, (bounds.Y - center.Y) * Scale, bounds.Width * Scale, bounds.Height * Scale);
        var restBounds = new Rect((rest.X - center.X) * Scale, (rest.Y - center.Y) * Scale, rest.Width * Scale, rest.Height * Scale);
        var area = Screens.ScreenFromWindow(this)?.WorkingArea;
        var bottom = area is { } work ? (work.Bottom - _origin.Y) / RenderScaling : Height;
        _dropCenter = _lastCenter;
        _drop = new PetDrop(_lastAngle, _interaction.AngularVelocity, _lastScale, _interaction.ReleaseVelocity / RenderScaling,
            sourceBounds, restBounds, SpriteSize, Math.Max(0, Math.Min(SpriteSize * 0.3, bottom - _dropCenter.Y - sourceBounds.Bottom)));
        _released = now; _dropOrigin = _origin;
        var end = _drop.At(_drop.Duration);
        _settle = new PixelPoint(_origin.X + (int)((_dropCenter.X + end.Offset.X - MarginCanvas - center.X * Scale) * RenderScaling),
            _origin.Y + (int)((_dropCenter.Y + end.Offset.Y - MarginCanvas - center.Y * Scale) * RenderScaling));
    }
    private void Draw(object? sender, EventArgs e)
    {
        var elapsed = _clock.Elapsed;
        _held = _interaction.HoldAt(elapsed);
        _state = _held?.State ?? _interaction.StateAt(elapsed) ?? _activity.StateAt(DateTimeOffset.Now);
        PetDropPose? drop = _drop?.At(elapsed - _released);
        if (drop is { } falling) _state = falling.Phase == PetDropPhase.Impact && falling.Landing == PetLanding.Head ? PetState.ProtectEyes : PetState.Active;
        if (_player.Draw(_state, elapsed, _interaction.AnimationRevision))
        {
            using var buffer = _bitmap.Lock();
            for (var y = 0; y < 320; y++) Marshal.Copy(_player.Buffer, y * 320 * 4, buffer.Address + y * buffer.RowBytes, 320 * 4);
        }
        var motion = PetAnimation.MotionAt(elapsed);
        var center = _appearance.RestCenter;
        var angle = motion.Angle; var sx = motion.ScaleX; var sy = motion.ScaleY;
        var target = new Point(MarginCanvas + center.X * Scale + motion.X * SpriteSize, MarginCanvas + center.Y * Scale + motion.Y * SpriteSize);
        var pivot = center;
        if (_held is { } hold)
        {
            angle = hold.Angle; sx = sy = 1; pivot = hold.Anchor;
            var screen = new PixelPoint((int)_interaction.PointerScreen.X, (int)_interaction.PointerScreen.Y);
            // Move the containing window, then rotate about the precise grabbed artwork point.
            _origin = new PixelPoint(screen.X - (int)((MarginCanvas + pivot.X * Scale) * RenderScaling), screen.Y - (int)((MarginCanvas + pivot.Y * Scale) * RenderScaling));
            target = new Point(MarginCanvas + pivot.X * Scale, MarginCanvas + pivot.Y * Scale);
        }
        else if (drop is { } pose)
        {
            _origin = _dropOrigin; angle = pose.Angle; sx = pose.ScaleX; sy = pose.ScaleY; target = _dropCenter + pose.Offset;
            if (pose.IsSettled) { _drop = null; _origin = _settle; Save(); target = new Point(MarginCanvas + center.X * Scale, MarginCanvas + center.Y * Scale); }
        }
        var matrix = Matrix.CreateTranslation(-pivot.X, -pivot.Y) * Matrix.CreateScale(Scale * sx, Scale * sy)
            * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(target.X, target.Y);
        // Keep the native window close to the painted silhouette. Transparent animation margins
        // must not cover a large, invisible rectangle of the person's desktop.
        var painted = PetPixels.Bounds(_player.Pixels.Span, 320).TransformToAABB(matrix).Inflate(4);
        Width = Math.Max(16, painted.Width); Height = Math.Max(16, painted.Height);
        Position = new PixelPoint(_origin.X + (int)(painted.X * RenderScaling), _origin.Y + (int)(painted.Y * RenderScaling));
        _image.RenderTransform = new MatrixTransform(matrix * Matrix.CreateTranslation(-painted.X, -painted.Y));
        _lastCenter = matrix.Transform(center); _lastAngle = angle; _lastScale = new Vector(sx, sy);
        _lastMatrix = matrix;
    }
    private void Save() => _placement.Save(new PetPlacement(_origin.X, _origin.Y));
}

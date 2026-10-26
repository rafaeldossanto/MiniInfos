using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

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
    private readonly DispatcherTimer _animation = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private PetFrame? _frame;
    private PetState? _state;

    public PetWindow(Preferences preferences, PetActivity activity, Action showCards, Action openSettings, Action quit)
    {
        _preferences = preferences;
        _activity = activity;
        _showCards = showCards;
        _openSettings = openSettings;
        _quit = quit;
        InitializeComponent();
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
            Paint();
            _animation.Start();
        };
        _animation.Tick += (_, _) => Paint();
        Closed += (_, _) => { _animation.Stop(); SavePosition(); };
        Paint();
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

    private void Paint()
    {
        var state = State;
        var next = PetSprites.Frame(state, PetSprites.IndexAt(state, Stopwatch.GetElapsedTime(_startedAt)));
        if (next != _frame) { _frame = next; Sprite.Source = next.Source; }
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
        if (message != 0x0084 || _frame is null) return 0; // WM_NCHITTEST
        var value = lParam.ToInt64();
        var local = PointFromScreen(new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF)));
        var pixel = new Point(local.X / Width * PetFrame.CanvasSize, local.Y / Height * PetFrame.CanvasSize);
        if (_frame.Hits(pixel)) return 0;
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

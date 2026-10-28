using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MiniInfos.UI.Controls;

/// <summary>The same themed artwork, title and controls on every card.</summary>
public partial class CardHeader : Grid
{
    public CardHeader()
    {
        InitializeComponent();
        HeaderArt.SizeChanged += (_, e) =>
        {
            var inset = HeaderArt.Tag is true;
            var radius = inset ? 8 : 15;
            HeaderArt.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height + (inset ? 0 : radius)), radius, radius);
        };
        RefreshButton.ToolTip = L.Text("Atualizar agora");
        DismissButton.ToolTip = L.Text("Esconder");
    }

    public event RoutedEventHandler? RefreshRequested;
    public event RoutedEventHandler? DismissRequested;

    public string Title { set => NumericText.Show(AppTitle, value); }
    public object RefreshToolTip { set => RefreshButton.ToolTip = value; }
    public object DismissToolTip { set => DismissButton.ToolTip = value; }

    public void SetRefreshing(bool refreshing) => RefreshSpin.BeginAnimation(RotateTransform.AngleProperty,
        refreshing ? new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(800)) { RepeatBehavior = RepeatBehavior.Forever } : null);

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, e);
    private void OnDismiss(object sender, RoutedEventArgs e) => DismissRequested?.Invoke(this, e);
}

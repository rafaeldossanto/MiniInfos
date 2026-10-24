using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace MiniInfos;

/// <summary>
/// The icon beside the clock: a click shows or hides the card, hovering reads the numbers
/// even while the card is hidden, and its menu holds what the card's own menu does.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int MaxTipLength = 127;
    private static readonly Uri IconResource = new("pack://application:,,,/MiniInfos;component/MiniInfos.ico");

    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggleItem;

    public TrayIcon(MainWindow window)
    {
        _window = window;
        _toggleItem = new Forms.ToolStripMenuItem("Mostrar", null, (_, _) => Toggle());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add("Atualizar agora", null, async (_, _) => await window.ForceRefreshAsync());
        menu.Items.Add("Voltar pro canto", null, (_, _) => window.BackToCorner());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair do MiniInfos", null, (_, _) => window.Quit());
        menu.Opening += (_, _) => _toggleItem.Text = window.IsVisible ? "Esconder" : "Mostrar";

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "MiniInfos",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) Toggle();
        };
        window.SummaryChanged += ShowSummary;
    }

    public void Dispose()
    {
        _window.SummaryChanged -= ShowSummary;
        _icon.Visible = false;
        _icon.Dispose();
    }

    private void ShowSummary(string summary) =>
        _icon.Text = summary.Length <= MaxTipLength ? summary : summary[..MaxTipLength];

    private void Toggle()
    {
        if (_window.IsVisible) _window.HideCard();
        else _window.BringBack();
    }

    private static Drawing.Icon LoadIcon()
    {
        using var stream = Application.GetResourceStream(IconResource).Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}

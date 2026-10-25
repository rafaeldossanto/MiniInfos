using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace MiniInfos;

/// <summary>
/// The icon beside the clock: a click shows or hides the cards, hovering reads the main card's
/// numbers even while it is hidden, and its menu holds what the card's own menu does.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int MaxTipLength = 127;
    private static readonly Uri IconResource = new("pack://application:,,,/MiniInfos;component/MiniInfos.ico");

    private readonly CardSet _cards;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggleItem;

    public TrayIcon(CardSet cards)
    {
        _cards = cards;
        _toggleItem = new Forms.ToolStripMenuItem("Mostrar", null, (_, _) => Toggle());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add("Atualizar agora", null, async (_, _) => await cards.ForceRefreshAsync());
        menu.Items.Add("Voltar pro canto", null, (_, _) => cards.Main.BackToCorner());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair do MiniInfos", null, (_, _) => cards.Quit());
        menu.Opening += (_, _) => _toggleItem.Text = cards.IsVisible ? "Esconder" : "Mostrar";

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
        cards.Main.SummaryChanged += ShowSummary;
    }

    public void Dispose()
    {
        _cards.Main.SummaryChanged -= ShowSummary;
        _icon.Visible = false;
        _icon.Dispose();
    }

    private void ShowSummary(string summary) =>
        _icon.Text = summary.Length <= MaxTipLength ? summary : summary[..MaxTipLength];

    private void Toggle()
    {
        if (_cards.IsVisible) _cards.Hide();
        else _cards.BringBack();
    }

    private static Drawing.Icon LoadIcon()
    {
        using var stream = Application.GetResourceStream(IconResource).Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}

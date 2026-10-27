using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using MiniInfos.UI.Windows;

namespace MiniInfos.Infrastructure.Windows;

/// <summary>
/// The icon beside the clock: a click shows or hides the cards, hovering reads the main card's
/// numbers even while it is hidden, and its menu holds what the card's own menu does.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int MaxTipLength = 127;
    private static readonly Uri IconResource = new("pack://application:,,,/MiniInfos;component/Assets/MiniInfos.ico");

    private readonly CardSet _cards;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggleItem;

    public TrayIcon(CardSet cards)
    {
        _cards = cards;
        _toggleItem = new Forms.ToolStripMenuItem(L.Text("Mostrar"), null, (_, _) => Toggle());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        AddTranslated("Atualizar agora", async (_, _) => await cards.ForceRefreshAsync());
        AddTranslated("Voltar pro canto", (_, _) => cards.Main.BackToCorner());
        AddTranslated("Configurações", (_, _) => cards.OpenSettings(cards.Main));
        menu.Items.Add(new Forms.ToolStripSeparator());
        AddTranslated("Sair do MiniInfos", (_, _) => cards.Quit());
        menu.Opening += (_, _) =>
        {
            foreach (Forms.ToolStripItem item in menu.Items)
                if (item.Tag is string source) item.Text = L.Text(source);
            _toggleItem.Text = cards.IsVisible ? L.Text("Esconder") : L.Text("Mostrar");
        };

        void AddTranslated(string source, EventHandler action)
        {
            var item = menu.Items.Add(L.Text(source), null, action);
            item.Tag = source;
        }

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

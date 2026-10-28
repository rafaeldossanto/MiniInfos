using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniInfos.Providers;
using MiniInfos.UI.Formatting;

namespace MiniInfos.UI.Controls;

/// <summary>A stable row per conversation: updates keep its rolling count and the list's scroll position.</summary>
public partial class ConversationView : Grid
{
    private static readonly TimeSpan LiveFor = TimeSpan.FromMinutes(2);
    private readonly RollingCount _tokens;

    public ConversationView()
    {
        InitializeComponent();
        _tokens = new RollingCount(Tokens);
        Unloaded += (_, _) => _tokens.Reset();
    }

    public void Show(Conversation conversation, DateTimeOffset now, Func<long, string> count, bool first, bool last)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(conversation.LastAt);
        var ago = Format.Ago(at, now);
        Icon.Data = TryFindResource($"ProviderIcon.{conversation.ProviderId}") as Geometry ?? (Geometry)FindResource("ProviderIcon.default");
        Icon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, first ? "PrimaryText" : "DimText");
        NumericText.Show(Title, conversation.Title);
        Title.ToolTip = conversation.Title;
        var where = conversation.Project is { } project ? $"{project} · {ago}" : ago;
        NumericText.Show(Where, where);
        Where.ToolTip = where;
        Live.Visibility = now - at < LiveFor ? Visibility.Visible : Visibility.Collapsed;
        _tokens.Show(conversation.Tokens.Total, count);
        Tokens.ToolTip = Format.Breakdown(conversation.Tokens, count);
        NumericText.Show(Context, conversation.ContextPercent is { } fill ? L.Format("contexto {0}", Format.Percent(fill)) : "");
        Divider.Visibility = last ? Visibility.Collapsed : Visibility.Visible;
    }
}

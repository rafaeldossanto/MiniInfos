namespace MiniInfos.UiCheck;

internal static partial class Program
{
    /// <summary>
    /// The conversations card: opened from the button beside the gear, the one that answered last on top, a provider's
    /// mark filtering the list, both number views, and the main-card choice deciding which card opens with the apps.
    /// </summary>
    private static void CheckConversations(CardSet cards, Preferences preferences, SettingsWindow settings, string pictures)
    {
        var card = cards.Main;
        var gear = Find<Button>(card, "SettingsButton");
        var opener = Find<Button>(card, "ConversationsButton");
        Check(opener.TranslatePoint(new Point(), card).X > gear.TranslatePoint(new Point(), card).X,
            "the conversations button sits beside the gear, which keeps its place");

        opener.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var window = cards.Conversations;
        PumpUntil(() => window.IsVisible, TimeSpan.FromSeconds(5));
        Pump(TimeSpan.FromMilliseconds(300));
        var all = window.Shown;
        var chats = cards.Providers.Sum(provider => provider.Conversations().Count(conversation => conversation.Tokens.Total > 0));
        Check(window.IsVisible && all.Count == Math.Min(chats, ConversationList.Limit),
            $"the button opens the conversations card with every installed provider's chats, the latest {ConversationList.Limit} at most ({all.Count} of {chats})");
        // With live data whoever answered last varies; the samples make Claude's the latest.
        var samples = cards.Providers.All(provider => provider is SampleProvider);
        Check(all.Zip(all.Skip(1)).All(pair => pair.First.LastAt >= pair.Second.LastAt) && (!samples || all[0].ProviderId == "claude"),
            "the chat that answered last comes first, the others after it by time");
        Check(Find<ItemsControl>(window, "List").Items.Count == all.Count, "one line per chat");
        Render(window, System.IO.Path.Combine(pictures, "conversations.png"));

        window.FilterBy("codex");
        Pump(TimeSpan.FromMilliseconds(200));
        Check(window.Shown.Count > 0 && window.Shown.All(conversation => conversation.ProviderId == "codex"), "a provider's mark filters the list to its chats");
        window.FilterBy("codex");
        Pump(TimeSpan.FromMilliseconds(200));
        Check(window.Filter is null && window.Shown.Count == all.Count, "pressed again, every provider's chats come back");

        var summary = Find<TextBlock>(window, "Summary");
        var compact = summary.Text;
        Find<RadioButton>(window, "DetailedView").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(200));
        var detailed = summary.Text;
        Find<RadioButton>(window, "CompactView").IsChecked = true;
        Pump(TimeSpan.FromMilliseconds(200));
        Check(compact != detailed && detailed.Count(char.IsDigit) > compact.Count(char.IsDigit),
            $"Detalhado writes every digit, Resumido the short form (\"{compact}\" / \"{detailed}\")");

        var choices = Find<StackPanel>(settings, "MainCardChoices").Children.OfType<RadioButton>().ToArray();
        Check(choices.Length == 2 && choices.Single(choice => (string)choice.Tag == "Usage").IsChecked == true,
            "Configurações offers Uso and Conversas as the main card, Uso by default");
        choices.Single(choice => (string)choice.Tag == "Conversations").IsChecked = true;
        Check(preferences.MainCard == MainCardChoice.Conversations && Preferences.Load().MainCard == MainCardChoice.Conversations,
            "choosing Conversas as main is saved");

        cards.Hide();
        Pump(TimeSpan.FromMilliseconds(200));
        Check(!window.IsVisible && !card.IsVisible, "the cards hide together, as when the last app closes");
        cards.ShowWithAssistant();
        Pump(TimeSpan.FromMilliseconds(300));
        Check(window.IsVisible && !card.IsVisible, "with Conversas as main, the conversations card is the one that opens with the apps");
        Find<Button>(window, "UsageButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(TimeSpan.FromMilliseconds(300));
        Check(card.IsVisible, "the button beside its gear opens the usage card");

        choices.Single(choice => (string)choice.Tag == "Usage").IsChecked = true;
        cards.Hide();
        Pump(TimeSpan.FromMilliseconds(200));
        cards.ShowWithAssistant();
        Pump(TimeSpan.FromMilliseconds(300));
        Check(preferences.MainCard == MainCardChoice.Usage && card.IsVisible && !window.IsVisible,
            "with Uso as main, the usage card opens and the conversations card waits for its button");
        CheckConversationParity(cards, preferences, pictures);
    }
}

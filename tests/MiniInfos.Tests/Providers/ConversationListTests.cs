using MiniInfos.Providers;

namespace MiniInfos.Tests.Providers;

public sealed class ConversationListTests
{
    [Fact]
    public void TheConversationThatAnsweredLastComesFirstAcrossProviders()
    {
        var list = ConversationList.Recent([
            new Fake("claude", Chat("claude", "a", 100), Chat("claude", "b", 300)),
            new Fake("codex", Chat("codex", "c", 200)),
        ]);

        Assert.Equal(["b", "c", "a"], list.Select(conversation => conversation.Key));
    }

    [Fact]
    public void AProviderFilterKeepsOnlyItsConversations()
    {
        var list = ConversationList.Recent([new Fake("claude", Chat("claude", "a", 100)), new Fake("kimi", Chat("kimi", "k", 50))], "kimi");

        Assert.Equal("k", Assert.Single(list).Key);
    }

    [Fact]
    public void ConversationsWithNoTokensAreLeftOutAndTheListIsCapped()
    {
        var many = Enumerable.Range(0, ConversationList.Limit + 10).Select(index => Chat("claude", $"c{index}", index)).ToArray();
        var list = ConversationList.Recent([new Fake("claude", [.. many, Chat("claude", "empty", 999, tokens: 0)])]);

        Assert.Equal(ConversationList.Limit, list.Count);
        Assert.DoesNotContain(list, conversation => conversation.Key == "empty");
        Assert.Equal($"c{ConversationList.Limit + 9}", list[0].Key);
    }

    [Fact]
    public void AProviderThatKeepsNoConversationsListsNone() =>
        Assert.Empty(((IUsageProvider)new Plain()).Conversations());

    private static Conversation Chat(string provider, string key, long at, long tokens = 10) =>
        new(provider, key, key, null, at, new TokenCount(tokens, 0, 0, 0));

    private sealed class Fake(string id, params Conversation[] conversations) : IUsageProvider
    {
        public string Id => id;
        public bool IsInstalled() => true;
        public bool IsInUse() => false;
        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force) => throw new NotSupportedException();
        public IReadOnlyList<Conversation> Conversations() => conversations;
        public void Flush() { }
    }

    /// <summary>A provider written before conversations existed: the interface's default answers for it.</summary>
    private sealed class Plain : IUsageProvider
    {
        public string Id => "plain";
        public bool IsInstalled() => true;
        public bool IsInUse() => false;
        public Task<UsagePage> ReadAsync(DateTimeOffset now, bool isShown, bool force) => throw new NotSupportedException();
        public void Flush() { }
    }
}

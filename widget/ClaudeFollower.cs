namespace MiniInfos;

/// <summary>
/// Shows the card when Claude opens (the desktop app, or a Claude Code chat in a terminal or
/// an editor) and hides it when the last one closes. Only a change counts, so a card the
/// person hid stays hidden until Claude is opened again.
/// </summary>
public sealed class ClaudeFollower(Func<bool> isClaudeOpen, Action show, Action hide)
{
    private bool? _wasOpen;

    public void Check()
    {
        var isOpen = isClaudeOpen();
        if (isOpen == _wasOpen) return;

        var isFirstCheck = _wasOpen is null;
        _wasOpen = isOpen;

        if (isOpen) show();
        else if (!isFirstCheck) hide();
    }
}

namespace MiniInfos;

/// <summary>
/// Shows the card when a supported assistant app opens and hides it after the last one closes.
/// Only a change counts, so a card the person hid stays hidden until an app opens again.
/// </summary>
public sealed class AssistantFollower(Func<bool> isAssistantOpen, Action show, Action hide)
{
    private bool? _wasOpen;

    public void Check()
    {
        var isOpen = isAssistantOpen();
        if (isOpen == _wasOpen) return;

        var isFirstCheck = _wasOpen is null;
        _wasOpen = isOpen;

        if (isOpen) show();
        else if (!isFirstCheck) hide();
    }
}

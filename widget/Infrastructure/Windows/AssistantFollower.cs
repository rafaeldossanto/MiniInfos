namespace MiniInfos.Infrastructure.Windows;

/// <summary>
/// Shows the card when a supported assistant app opens and hides it after the last one closes.
/// Only a change counts, so a card the person hid stays hidden until an app opens again.
/// </summary>
public sealed class AssistantFollower(Func<bool> isAssistantOpen, Action show, Action hide)
{
    private bool? _wasOpen;
    private bool _isEnabled = true;

    /// <summary>Off, it leaves the cards alone. Turned back on, it starts over: it shows them if an app is open and hides nothing at once.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value && !_isEnabled) _wasOpen = null;
            _isEnabled = value;
        }
    }

    public void Check()
    {
        if (!_isEnabled) return;

        var isOpen = isAssistantOpen();
        if (isOpen == _wasOpen) return;

        var isFirstCheck = _wasOpen is null;
        _wasOpen = isOpen;

        if (isOpen) show();
        else if (!isFirstCheck) hide();
    }
}

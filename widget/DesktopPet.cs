using System.ComponentModel;

namespace MiniInfos;

/// <summary>Owns at most one pet window and follows the shared settings, independently of card visibility.</summary>
public sealed class DesktopPet : IDisposable
{
    private readonly Preferences _preferences;
    private readonly Action _showCards;
    private readonly Action _openSettings;
    private readonly Action _quit;
    private readonly PetActivity _activity = new();

    public DesktopPet(Preferences preferences, Action showCards, Action openSettings, Action quit)
    {
        _preferences = preferences;
        _showCards = showCards;
        _openSettings = openSettings;
        _quit = quit;
        preferences.PropertyChanged += OnPreferenceChanged;
        Follow();
    }

    public PetWindow? Window { get; private set; }

    public void Observe(IReadOnlyList<PetUsage> usage, DateTimeOffset now) => _activity.Observe(usage, now);

    public void Dispose()
    {
        _preferences.PropertyChanged -= OnPreferenceChanged;
        CloseWindow();
    }

    private void OnPreferenceChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName == nameof(Preferences.PetEnabled)) Follow();
        if (change.PropertyName == nameof(Preferences.PetSize)) Window?.ResizePet();
    }

    private void Follow()
    {
        if (!_preferences.PetEnabled) { CloseWindow(); return; }
        if (Window is not null) return;
        var window = new PetWindow(_preferences, _activity, _showCards, _openSettings, _quit);
        Window = window;
        window.Closed += (_, _) =>
        {
            if (Window == window) Window = null;
        };
        window.Show();
    }

    private void CloseWindow()
    {
        Window?.Close();
    }
}

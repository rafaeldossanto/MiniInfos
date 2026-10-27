using System.Windows.Controls;
using System.Windows;
using MiniInfos.Settings;

namespace MiniInfos.UI.Windows;

public partial class SettingsWindow
{
    private bool _showingLanguage;

    private void ShowLanguage()
    {
        _showingLanguage = true;
        foreach (var item in LanguageChoices.Children.OfType<RadioButton>())
            item.IsChecked = (string)item.Tag == _preferences.Language.ToString();
        _showingLanguage = false;
    }

    private void OnLanguageChosen(object sender, RoutedEventArgs e)
    {
        if (!_showingLanguage && sender is RadioButton item)
            _preferences.Language = Enum.Parse<LanguageChoice>((string)item.Tag);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        VersionText.Text = L.Format("Versão {0}", MiniInfos.Infrastructure.AppInfo.Version);
        ShowLanguage();
        ShowFonts();
        RefreshCommunity(refreshLibraries: false);
        ShowPetPreview();
        if (_catalogSnapshot is not null && !_catalogInstalling) ShowCatalogChoices();
    }
}

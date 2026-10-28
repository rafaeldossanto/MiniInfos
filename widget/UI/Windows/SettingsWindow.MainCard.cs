using System.Windows;
using System.Windows.Controls;
using MiniInfos.Settings;

namespace MiniInfos.UI.Windows;

public partial class SettingsWindow
{
    private bool _showingMainCard;

    private void ShowMainCard()
    {
        _showingMainCard = true;
        foreach (var item in MainCardChoices.Children.OfType<RadioButton>())
            item.IsChecked = (string)item.Tag == _preferences.MainCard.ToString();
        _showingMainCard = false;
    }

    private void OnMainCardChosen(object sender, RoutedEventArgs e)
    {
        if (!_showingMainCard && sender is RadioButton item)
            _preferences.MainCard = Enum.Parse<MainCardChoice>((string)item.Tag);
    }
}

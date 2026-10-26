using System.Windows.Controls;
using System.Windows;

namespace MiniInfos.UI.Controls;

/// <summary>The card's shared menu theme, including items created in code and their submenus.</summary>
public sealed class CardContextMenu : ContextMenu
{
    public CardContextMenu()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/MiniInfos;component/UI/Styles/MenuStyles.xaml", UriKind.Absolute),
        });
        SetResourceReference(StyleProperty, typeof(ContextMenu));
    }
}

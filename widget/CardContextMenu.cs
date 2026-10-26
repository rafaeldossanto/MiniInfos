using System.Windows;
using System.Windows.Controls;

namespace MiniInfos;

/// <summary>The card's shared menu theme, including items created in code and their submenus.</summary>
public sealed class CardContextMenu : ContextMenu
{
    public CardContextMenu()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/MiniInfos;component/MenuStyles.xaml", UriKind.Absolute),
        });
        SetResourceReference(StyleProperty, typeof(ContextMenu));
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MiniInfos.UI.Windows;

/// <summary>
/// A small window over the settings, in their look: the title bar drags it, and ✕ or Esc close it. The frame is the
/// DialogChrome template in SettingsStyles.xaml, merged here with the rest of those styles; a dialog brings only its content.
/// </summary>
public class DialogWindow : Window
{
    public DialogWindow()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/MiniInfos;component/UI/Styles/SettingsStyles.xaml", UriKind.Absolute),
        });
        Template = (ControlTemplate)Resources["DialogChrome"];
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontSize = 12;
        SetResourceReference(FontFamilyProperty, "AppFont");
        SetResourceReference(ForegroundProperty, "PrimaryText");
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_TitleBar") is UIElement bar)
            bar.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        if (GetTemplateChild("PART_Close") is Button close) close.Click += (_, _) => Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.Escape) return;

        Close();
        e.Handled = true;
    }
}

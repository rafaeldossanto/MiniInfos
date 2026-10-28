using System.Windows;

namespace MiniInfos.UI.Windows;

/// <summary>The actions in "Este cartão", independent of what a card displays.</summary>
public interface ISettingsCard
{
    Window Window { get; }
    bool IsDetailed { get; set; }
    event Action? ViewChanged;
    void BringBack();
    void BackToCorner();
    void ResetSize();
}

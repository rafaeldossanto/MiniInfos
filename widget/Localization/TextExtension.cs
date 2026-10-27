using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MiniInfos.Localization;

/// <summary>A live translation binding; weak listeners release closed windows and discarded controls.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(Translation.Value)) { Source = new Translation(text), Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);

    private sealed class Translation : INotifyPropertyChanged
    {
        private readonly string _source;
        public Translation(string source)
        {
            _source = source;
            PropertyChangedEventManager.AddHandler(Localizer.Current, OnCultureChanged, nameof(Localizer.ActiveCulture));
        }
        public string Value => Localizer.Text(_source);
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnCultureChanged(object? sender, PropertyChangedEventArgs change) =>
            PropertyChanged?.Invoke(this, new(nameof(Value)));
    }
}

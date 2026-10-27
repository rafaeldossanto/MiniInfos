using System.Text.Json.Serialization;

namespace MiniInfos.Settings;

/// <summary>The cards' colors: one of the dictionaries in UI/Themes/, or the one Windows' own mode picks.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThemeChoice>))]
public enum ThemeChoice
{
    /// <summary>The plain dark card, with blue bars.</summary>
    Simple,

    /// <summary>The dark, illustrated Athena edition of the card, in stone tones.</summary>
    Classic,

    Light,

    /// <summary>Simple or Light as Windows' app mode is, changing along with it.</summary>
    System,

    /// <summary>The illustrated Athena card in ivory marble and warm light stone tones.</summary>
    ClassicLight,
}

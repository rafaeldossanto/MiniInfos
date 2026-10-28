using System.Text.Json.Serialization;

namespace MiniInfos.Settings;

/// <summary>
/// The card that opens on its own: with an AI app, with Windows, from the tray and when the app is opened again.
/// The other one opens from its button beside the gear.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MainCardChoice>))]
public enum MainCardChoice
{
    /// <summary>The usage card: limits and tokens, one provider per page.</summary>
    Usage,

    /// <summary>The conversations card: each chat with its tokens, the one that answered last first.</summary>
    Conversations,
}

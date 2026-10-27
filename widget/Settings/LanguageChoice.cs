using System.Text.Json.Serialization;

namespace MiniInfos.Settings;

[JsonConverter(typeof(JsonStringEnumConverter<LanguageChoice>))]
public enum LanguageChoice
{
    Automatic,
    Portuguese,
    English,
}

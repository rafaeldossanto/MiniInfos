using System.Text.Json.Serialization;

namespace MiniInfos.Settings;

/// <summary>Embedded families, stored by name so future additions do not change saved choices.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FontChoice>))]
public enum FontChoice
{
    Manrope,
    GeistMono,
    Cinzel,
    BodoniModa,
    ChakraPetch,
    Orbitron,
}

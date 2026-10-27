using System.Runtime.CompilerServices;
using MiniInfos.Localization;
using MiniInfos.Settings;

namespace MiniInfos.Tests.Support;

internal static class LanguageDefaults
{
    // Existing behavior checks use Portuguese fixtures; localization checks choose their language explicitly.
    [ModuleInitializer]
    internal static void Initialize() => Localizer.Select(LanguageChoice.Portuguese);
}

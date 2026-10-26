@{
    # One map for local runs and CI. Rules use the first matching path, most specific first.
    Domains = @{
        Community = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Community.'; Suite = 'Community' }
        Mascot = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Mascot.'; Suite = 'Mascot' }
        Claude = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.Claude.'; Suite = '' }
        Codex = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.Codex.'; Suite = '' }
        Providers = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.PlanWindowTests|FullyQualifiedName~MiniInfos.Tests.Providers.TokenSummaryTests|FullyQualifiedName~MiniInfos.Tests.Providers.InstalledProvidersTests'; Suite = '' }
        Integration = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.'; Suite = 'Providers' }
        Sources = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Sources.'; Suite = '' }
        Settings = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Settings.'; Suite = 'Settings' }
        Themes = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Settings.'; Suite = 'Themes' }
        Fonts = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.UI.Themes.'; Suite = 'Fonts' }
        Cards = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.UI.Windows.'; Suite = 'Cards' }
        Formatting = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.UI.Formatting.'; Suite = '' }
        Updates = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Updates.'; Suite = 'Updates' }
        Storage = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Infrastructure.Storage.'; Suite = '' }
        Windows = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Infrastructure.Windows.'; Suite = '' }
    }
    Rules = @(
        @{ Path = '*.md'; Ignore = $true }
        @{ Path = '.gitignore'; Ignore = $true }
        @{ Path = 'widget/Community/*'; Domains = @('Community', 'Themes', 'Mascot', 'Settings') }
        @{ Path = 'community/*'; Domains = @('Community') }
        @{ Path = 'widget/Mascot/PetAnatomy.cs'; Domains = @('Community', 'Mascot') }
        @{ Path = 'widget/Mascot/PetAppearances.cs'; Domains = @('Community', 'Mascot') }
        @{ Path = 'widget/UI/Themes/CommunityTheme.cs'; Domains = @('Community', 'Themes') }
        @{ Path = 'widget/UI/Windows/PetWindow.*'; Domains = @('Mascot') }
        @{ Path = 'widget/UI/Windows/UpdateBanner.*'; Domains = @('Updates') }
        @{ Path = 'widget/UI/Windows/SettingsWindow.*'; Domains = @('Settings', 'Themes', 'Fonts', 'Mascot') }
        @{ Path = 'widget/UI/Windows/*'; Domains = @('Cards', 'Settings', 'Themes', 'Fonts', 'Mascot', 'Updates') }
        @{ Path = 'widget/UI/Themes/AppFonts.cs'; Domains = @('Fonts') }
        @{ Path = 'widget/UI/Themes/*'; Domains = @('Themes') }
        @{ Path = 'widget/UI/Controls/CardContextMenu.cs'; Domains = @('Cards', 'Themes', 'Mascot') }
        @{ Path = 'widget/UI/Controls/*'; Domains = @('Cards', 'Fonts', 'Updates') }
        @{ Path = 'widget/UI/Styles/MenuStyles.xaml'; Domains = @('Cards', 'Themes', 'Mascot') }
        @{ Path = 'widget/UI/Styles/*'; Domains = @('Cards', 'Settings', 'Themes', 'Fonts', 'Mascot', 'Updates') }
        @{ Path = 'widget/UI/Icons/*'; Domains = @('Cards') }
        @{ Path = 'widget/UI/Formatting/*'; Domains = @('Formatting', 'Claude', 'Codex', 'Cards', 'Updates') }
        @{ Path = 'widget/Assets/Fonts/*'; Domains = @('Fonts') }
        @{ Path = 'widget/Assets/athena-owl-*'; Domains = @('Mascot') }
        @{ Path = 'widget/Assets/athena-header.png'; Domains = @('Themes') }
        @{ Path = 'widget/Assets/MiniInfos.ico'; Domains = @('Cards') }
        @{ Path = 'widget/Mascot/*'; Domains = @('Mascot') }
        @{ Path = 'widget/Providers/Claude/*'; Domains = @('Claude') }
        @{ Path = 'widget/Providers/Codex/*'; Domains = @('Codex') }
        @{ Path = 'widget/Providers/*'; Domains = @('Providers', 'Claude', 'Codex', 'Cards', 'Mascot') }
        @{ Path = 'widget/Sources/*'; Domains = @('Sources', 'Claude', 'Codex') }
        @{ Path = 'widget/Settings/FontChoice.cs'; Domains = @('Fonts', 'Settings') }
        @{ Path = 'widget/Settings/ThemeChoice.cs'; Domains = @('Themes', 'Settings') }
        @{ Path = 'widget/Settings/CardSettings.cs'; Domains = @('Cards') }
        @{ Path = 'widget/Settings/*'; Domains = @('Settings', 'Cards', 'Themes', 'Fonts', 'Mascot', 'Updates') }
        @{ Path = 'widget/Updates/*'; Domains = @('Updates') }
        @{ Path = 'widget/Infrastructure/Storage/JsonlTail.cs'; Domains = @('Storage', 'Claude', 'Codex') }
        @{ Path = 'widget/Infrastructure/Storage/SourceFiles.cs'; Domains = @('Storage', 'Sources', 'Claude', 'Codex') }
        @{ Path = 'widget/Infrastructure/Storage/CardStore.cs'; Domains = @('Cards', 'Storage') }
        @{ Path = 'widget/Infrastructure/Storage/*'; All = $true }
        @{ Path = 'widget/Infrastructure/Windows/AspectSizing.cs'; Domains = @('Windows', 'Cards') }
        @{ Path = 'widget/Infrastructure/Windows/StartupShortcut.cs'; Domains = @('Windows', 'Settings') }
        @{ Path = 'widget/Infrastructure/Windows/*'; Domains = @('Windows', 'Claude', 'Codex', 'Cards') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/Claude/*'; Domains = @('Claude') }
        @{ Path = 'tests/MiniInfos.Tests/Community/*'; Domains = @('Community') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/Codex/*'; Domains = @('Codex') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/*'; Domains = @('Providers') }
        @{ Path = 'tests/MiniInfos.Tests/Mascot/*'; Domains = @('Mascot') }
        @{ Path = 'tests/MiniInfos.Tests/Sources/*'; Domains = @('Sources') }
        @{ Path = 'tests/MiniInfos.Tests/Settings/*'; Domains = @('Settings') }
        @{ Path = 'tests/MiniInfos.Tests/Updates/*'; Domains = @('Updates') }
        @{ Path = 'tests/MiniInfos.Tests/UI/Windows/*'; Domains = @('Cards') }
        @{ Path = 'tests/MiniInfos.Tests/UI/Formatting/*'; Domains = @('Formatting') }
        @{ Path = 'tests/MiniInfos.Tests/UI/Themes/*'; Domains = @('Fonts') }
        @{ Path = 'tests/MiniInfos.Tests/Infrastructure/Storage/*'; Domains = @('Storage') }
        @{ Path = 'tests/MiniInfos.Tests/Infrastructure/Windows/*'; Domains = @('Windows') }
        @{ Path = 'tests/MiniInfos.UiCheck/Mascot/*'; Domains = @('Mascot') }
        @{ Path = 'tests/MiniInfos.UiCheck/Community/*'; Domains = @('Community') }
        @{ Path = 'tests/MiniInfos.UiCheck/Themes/*'; Domains = @('Themes') }
        @{ Path = 'tests/MiniInfos.UiCheck/Fonts/*'; Domains = @('Fonts') }
        @{ Path = 'tests/MiniInfos.UiCheck/Cards/*'; Domains = @('Cards') }
        @{ Path = 'tests/MiniInfos.UiCheck/Settings/*'; Domains = @('Settings') }
        @{ Path = 'tests/MiniInfos.UiCheck/Updates/*'; Domains = @('Updates') }
        @{ Path = 'tests/MiniInfos.UiCheck/Providers/*'; Domains = @('Integration') }
        @{ Path = 'tests/MiniInfos.UiCheck/*'; All = $true }
        @{ Path = 'tests/*.ps*'; All = $true }
        @{ Path = '.github/workflows/tests.yml'; All = $true }
        @{ Path = 'mod/*'; Mod = $true }
        @{ Path = '.claude-plugin/*'; Mod = $true }
        @{ Path = 'installer/*'; All = $true }
        # App bootstrap, project files, shared test helpers and unmapped files fall back to all tests.
    )
}

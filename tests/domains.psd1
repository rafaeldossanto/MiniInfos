@{
    # One map for local runs and CI. Rules use the first matching path, most specific first.
    Domains = @{
        Front = @{ Filter = ''; Suite = ''; Check = @{ Command = 'npm'; Arguments = @('--prefix', 'front', 'run', 'check') } }
        Localization = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Localization.|FullyQualifiedName~MiniInfos.Tests.Settings.|FullyQualifiedName~MiniInfos.Tests.UI.Formatting.'; Suite = 'Localization' }
        CatalogTool = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Community.CatalogToolTests'; Suite = '' }
        Community = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Community.'; Suite = 'Community' }
        Mascot = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Mascot.'; Suite = 'Mascot' }
        Claude = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.Claude.'; Suite = '' }
        Codex = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.Codex.'; Suite = '' }
        Gemini = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.Gemini.'; Suite = '' }
        Kimi = @{ Filter = 'FullyQualifiedName~MiniInfos.Tests.Providers.Kimi.'; Suite = '' }
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
        @{ Path = 'front/*'; Domains = @('Front') }
        @{ Path = 'widget/Localization/*'; Domains = @('Localization') }
        @{ Path = 'widget/Settings/LanguageChoice.cs'; Domains = @('Localization') }
        @{ Path = 'widget/UI/Windows/SettingsWindow.Language.cs'; Domains = @('Localization') }
        @{ Path = 'tests/MiniInfos.Tests/Localization/*'; Domains = @('Localization') }
        @{ Path = 'tests/MiniInfos.UiCheck/Localization/*'; Domains = @('Localization') }
        @{ Path = '.github/workflows/front.yml'; Domains = @('Front') }
        @{ Path = '.github/workflows/front-deploy.yml'; Domains = @('Front') }
        @{ Path = 'infra/*'; Domains = @('Front') }
        @{ Path = 'tools/MiniInfos.Catalog/*'; Domains = @('CatalogTool') }
        @{ Path = 'widget/Community/*'; Domains = @('Community', 'Themes', 'Mascot', 'Settings') }
        @{ Path = 'marketplace/*'; Domains = @('CatalogTool') }
        # Deleted paths stay mapped while a PR spans the community/ -> marketplace/ rename.
        @{ Path = 'community/*'; Domains = @('CatalogTool') }
        @{ Path = 'widget/Mascot/PetAnatomy.cs'; Domains = @('Community', 'Mascot') }
        @{ Path = 'widget/Mascot/PetAppearances.cs'; Domains = @('Community', 'Mascot') }
        @{ Path = 'widget/UI/Themes/CommunityTheme.cs'; Domains = @('Community', 'Themes') }
        @{ Path = 'widget/UI/Windows/PetWindow.*'; Domains = @('Mascot') }
        @{ Path = 'widget/UI/Windows/UpdateBanner.*'; Domains = @('Updates') }
        @{ Path = 'widget/UI/Windows/MascotExampleWindow.*'; Domains = @('Community', 'Mascot') }
        @{ Path = 'widget/UI/Windows/DialogWindow.cs'; Domains = @('Themes', 'Community', 'Mascot') }
        @{ Path = 'widget/UI/Windows/ThemeLibraryWindow.*'; Domains = @('Themes', 'Community') }
        @{ Path = 'widget/UI/Windows/SettingsWindow.*'; Domains = @('Settings', 'Themes', 'Fonts', 'Mascot', 'Community') }
        @{ Path = 'widget/UI/Windows/*'; Domains = @('Cards', 'Settings', 'Themes', 'Fonts', 'Mascot', 'Updates') }
        @{ Path = 'widget/UI/Themes/AppFonts.cs'; Domains = @('Fonts') }
        @{ Path = 'widget/UI/Themes/ThemeOption.cs'; Domains = @('Themes', 'Fonts', 'Community') }
        @{ Path = 'widget/UI/Themes/*'; Domains = @('Themes') }
        @{ Path = 'widget/UI/Controls/CardContextMenu.cs'; Domains = @('Cards', 'Themes', 'Mascot') }
        @{ Path = 'widget/UI/Controls/PackageCard.cs'; Domains = @('Community', 'Themes') }
        @{ Path = 'widget/UI/Controls/*'; Domains = @('Cards', 'Fonts', 'Updates') }
        @{ Path = 'widget/UI/Styles/MenuStyles.xaml'; Domains = @('Cards', 'Themes', 'Mascot') }
        @{ Path = 'widget/UI/Styles/SettingsStyles.xaml'; Domains = @('Cards', 'Settings', 'Themes', 'Fonts', 'Mascot', 'Updates', 'Community') }
        @{ Path = 'widget/UI/Styles/*'; Domains = @('Cards', 'Settings', 'Themes', 'Fonts', 'Mascot', 'Updates') }
        @{ Path = 'widget/UI/Icons/*'; Domains = @('Cards') }
        @{ Path = 'widget/UI/Formatting/*'; Domains = @('Formatting', 'Claude', 'Codex', 'Gemini', 'Kimi', 'Cards', 'Updates') }
        @{ Path = 'widget/Assets/Fonts/*'; Domains = @('Fonts') }
        @{ Path = 'widget/Assets/athena-owl-*'; Domains = @('Mascot') }
        @{ Path = 'widget/Assets/athena-header.png'; Domains = @('Themes') }
        @{ Path = 'widget/Assets/athena-banner-*.png'; Domains = @('Themes') }
        @{ Path = 'widget/Assets/MiniInfos.ico'; Domains = @('Cards') }
        @{ Path = 'widget/Mascot/*'; Domains = @('Mascot') }
        @{ Path = 'widget/Providers/Claude/*'; Domains = @('Claude') }
        @{ Path = 'widget/Providers/Codex/*'; Domains = @('Codex') }
        @{ Path = 'widget/Providers/Gemini/*'; Domains = @('Gemini') }
        @{ Path = 'widget/Providers/Kimi/*'; Domains = @('Kimi') }
        @{ Path = 'widget/Providers/*'; Domains = @('Providers', 'Claude', 'Codex', 'Gemini', 'Kimi', 'Cards', 'Mascot') }
        @{ Path = 'widget/Sources/*'; Domains = @('Sources', 'Claude', 'Codex', 'Kimi') }
        @{ Path = 'widget/Settings/FontChoice.cs'; Domains = @('Fonts', 'Settings') }
        @{ Path = 'widget/Settings/ThemeChoice.cs'; Domains = @('Themes', 'Settings') }
        @{ Path = 'widget/Settings/CardSettings.cs'; Domains = @('Cards') }
        @{ Path = 'widget/Settings/ThemeHistory.cs'; Domains = @('Settings', 'Themes', 'Fonts', 'Community') }
        @{ Path = 'widget/Settings/*'; Domains = @('Settings', 'Cards', 'Themes', 'Fonts', 'Mascot', 'Updates') }
        @{ Path = 'widget/Updates/*'; Domains = @('Updates') }
        @{ Path = 'widget/Infrastructure/Storage/JsonlTail.cs'; Domains = @('Storage', 'Claude', 'Codex', 'Gemini', 'Kimi') }
        @{ Path = 'widget/Infrastructure/Storage/SourceFiles.cs'; Domains = @('Storage', 'Sources', 'Claude', 'Codex', 'Kimi') }
        @{ Path = 'widget/Infrastructure/Storage/CardStore.cs'; Domains = @('Cards', 'Storage') }
        @{ Path = 'widget/Infrastructure/Storage/*'; All = $true }
        @{ Path = 'widget/Infrastructure/Windows/AspectSizing.cs'; Domains = @('Windows', 'Cards') }
        @{ Path = 'widget/Infrastructure/Windows/StartupShortcut.cs'; Domains = @('Windows', 'Settings') }
        @{ Path = 'widget/Infrastructure/Network/*'; Domains = @('Community', 'Updates') }
        @{ Path = 'widget/Infrastructure/Windows/*'; Domains = @('Windows', 'Claude', 'Codex', 'Gemini', 'Kimi', 'Cards') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/Claude/*'; Domains = @('Claude') }
        @{ Path = 'tests/MiniInfos.Tests/Community/CatalogToolTests.cs'; Domains = @('CatalogTool') }
        @{ Path = 'tests/MiniInfos.Tests/Community/*'; Domains = @('Community') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/Codex/*'; Domains = @('Codex') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/Gemini/*'; Domains = @('Gemini') }
        @{ Path = 'tests/MiniInfos.Tests/Providers/Kimi/*'; Domains = @('Kimi') }
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
        @{ Path = 'tools/*.ps1'; Domains = @('CatalogTool') }
        @{ Path = '.github/workflows/marketplace-*.yml'; Domains = @('CatalogTool') }
        @{ Path = '.gitattributes'; Domains = @('CatalogTool') }
        @{ Path = 'installer/*'; All = $true }
        # App bootstrap, project files, shared test helpers and unmapped files fall back to all tests.
    )
}

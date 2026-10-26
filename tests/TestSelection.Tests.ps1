$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TestSelection.psm1') -Force

function Assert-Selection([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$pet = Get-TestSelection -Paths @('widget/Mascot/PetInteraction.cs', 'widget/UI/Windows/PetWindow.xaml')
Assert-Selection (($pet.Domains -join ',') -eq 'Mascot') 'Pet isolado selecionou outro domínio'
Assert-Selection (($pet.Suites -join ',') -eq 'Mascot') 'Pet isolado selecionou outro cenário visual'
Assert-Selection (-not $pet.All) 'Pet isolado virou execução completa'
Assert-Selection (((Get-TestSelection -Paths @('tests/MiniInfos.UiCheck/Mascot/Checks.cs')).Domains -join ',') -eq 'Mascot') 'Cenário isolado virou regressão completa'
Assert-Selection ((Get-TestSelection -Paths @('tests/MiniInfos.UiCheck/Support/Checks.cs')).All) 'Auxiliar compartilhado perdeu consumidores'
Assert-Selection (((Get-TestSelection -Paths @('tests/MiniInfos.UiCheck/Providers/Checks.cs')).Suites -join ',') -eq 'Providers') 'Integração visual não selecionou seu próprio cenário'

$theme = Get-TestSelection -Paths @('widget/UI/Themes/Classic.xaml')
Assert-Selection (($theme.Suites -join ',') -eq 'Themes') 'Tema selecionou cenários não relacionados'
$community = Get-TestSelection -Paths @('widget/Community/PackageLibrary.cs')
Assert-Selection (($community.Domains -join ',') -eq 'Community,Mascot,Settings,Themes') 'Pacotes perderam consumidores ou selecionaram provedores não relacionados'

$tail = Get-TestSelection -Paths @('widget/Infrastructure/Storage/JsonlTail.cs')
Assert-Selection (($tail.Domains -join ',') -eq 'Claude,Codex,Storage') 'Leitura compartilhada não incluiu os dois provedores'

$prefs = Get-TestSelection -Paths @('widget/Settings/Preferences.cs')
Assert-Selection ($prefs.Domains -contains 'Mascot' -and $prefs.Domains -contains 'Themes') 'Preferências não incluíram seus consumidores'

$moved = Get-TestSelection -Paths @('widget/Mascot/old.cs', 'widget/Updates/new.cs')
Assert-Selection (($moved.Domains -join ',') -eq 'Mascot,Updates') 'Mudança entre domínios perdeu um dos lados'

$docs = Get-TestSelection -Paths @('README.md', 'AGENTS.md')
Assert-Selection ($docs.Domains.Count -eq 0) 'Documentação selecionou testes'
$mod = Get-TestSelection -Paths @('mod/hooks/register.ts')
Assert-Selection ($mod.Mod -and $mod.Domains.Count -eq 0) 'Mod selecionou testes do widget'
Assert-Selection ((Get-TestSelection -Paths @('.claude-plugin/marketplace.json')).Mod) 'Marketplace não selecionou o mod'
Assert-Selection ((Get-TestSelection -Paths @('.gitignore')).Domains.Count -eq 0) 'Gitignore selecionou testes'

$unknown = Get-TestSelection -Paths @('widget/NewFeature/NewFile.cs')
Assert-Selection ($unknown.All) 'Código sem regra foi ignorado'
Assert-Selection ((Get-TestSelection -Paths @('widget/MiniInfos.csproj')).All) 'Projeto não selecionou regressão completa'
Assert-Selection ((Get-TestSelection -All).All) 'Execução completa indisponível'

$failed = $false
try { Get-TestSelection -Domains @('Typo') | Out-Null } catch { $failed = $true }
Assert-Selection $failed 'Nome de domínio inválido foi ignorado'
Write-Host 'Seleção de testes: 17 cenários passaram.'

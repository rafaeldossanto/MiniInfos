[CmdletBinding()]
param([string]$Base = 'HEAD', [switch]$All)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../tests/TestSelection.psm1') -Force
$paths = @(Get-ChangedTestPaths -Base $Base)
$selection = Get-TestSelection -Paths $paths
$filter = $selection.Filter
$nativeUi = [bool]($All -or $selection.All)
if (-not $All -and -not $selection.All) {
    # Reuse the provider/source domain filters; WPF-only view files never select native UI checks.
    $nativeUi = @($paths | Where-Object { $_ -match '^desktop/|^tests/MiniInfos.Desktop.Tests/(?!Providers/)|^widget/(Settings|Mascot|Community|Localization|Assets)/|^widget/UI/(Themes|Formatting)/|^widget/Infrastructure/(Desktop|Storage|Network)/|^widget/Providers/[^/]+\.cs$' }).Count -gt 0
    if ($nativeUi) { $filter = (@($filter, 'FullyQualifiedName~MiniInfos.Tests.Desktop.') | Where-Object { $_ }) -join '|' }
    if (-not $filter) { Write-Host 'No affected native desktop tests.'; exit 0 }
}
if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value ('ui=' + $nativeUi.ToString().ToLowerInvariant()) }
$arguments = @('test', 'tests/MiniInfos.Desktop.Tests', '-c', 'Release', '--logger', 'console;verbosity=minimal')
if (-not $All -and -not $selection.All) { $arguments += @('--filter', $filter) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Native desktop checks failed.' }

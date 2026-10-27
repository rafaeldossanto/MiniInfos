# Defaults to working-tree changes against HEAD. Use -Base for a PR/push range, -Domain for an explicit scope.
[CmdletBinding(DefaultParameterSetName = 'Changed')]
param(
    [Parameter(ParameterSetName = 'Domain', Mandatory = $true)][string[]]$Domain,
    [Parameter(ParameterSetName = 'All', Mandatory = $true)][switch]$All,
    [Parameter(ParameterSetName = 'Changed')][string]$Base = 'HEAD',
    [switch]$Ui,
    [switch]$Plan,
    # The CI's .NET job: separate checks (the front) run in their own workflow, with their own toolchain.
    [switch]$SkipChecks
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TestSelection.psm1') -Force
$projectRoot = Split-Path $PSScriptRoot

function Invoke-Checked([string]$Command, [string[]]$CommandArguments) {
    & $Command @CommandArguments
    if ($LASTEXITCODE -ne 0) { throw "$Command falhou (código $LASTEXITCODE)" }
}

Push-Location -LiteralPath $projectRoot
try {
    $selection = if ($All) { Get-TestSelection -All }
        elseif ($Domain) { Get-TestSelection -Domains $Domain }
        else { Get-TestSelection -Paths @(Get-ChangedTestPaths -Base $Base) }
    if ($selection.Domains.Count -eq 0 -and -not $selection.Mod) {
        Write-Host 'Nenhuma mudança que exija testes.'
        exit 0
    }
    Write-Host ('Domínios: ' + ($selection.Domains -join ', '))
    if ($selection.Unmapped.Count -gt 0) { Write-Host ('Escopo completo: caminho sem regra: ' + ($selection.Unmapped -join ', ')) }
    if ($Plan) {
        Write-Host ('Unidade: ' + $(if ($selection.All) { 'todos' } else { $selection.Filter }))
        Write-Host ('Visual: ' + ($selection.Suites -join ', '))
        Write-Host ('Mod: ' + $selection.Mod)
        foreach ($check in $selection.Checks) { Write-Host ('Separado: ' + $check.Command + ' ' + ($check.Arguments -join ' ')) }
        exit 0
    }
    if ($selection.All -or $selection.Filter) {
        $testArguments = @('test', 'tests/MiniInfos.Tests', '-c', 'Release', '--logger', 'console;verbosity=minimal')
        if (-not $selection.All) { $testArguments += @('--filter', $selection.Filter) }
        Invoke-Checked 'dotnet' $testArguments
    }
    if ($SkipChecks) {
        foreach ($check in $selection.Checks) { Write-Host ('Separado, no workflow próprio: ' + $check.Command + ' ' + ($check.Arguments -join ' ')) }
    }
    else { foreach ($check in $selection.Checks) { Invoke-Checked $check.Command $check.Arguments } }
    if ($Ui -and $selection.Suites.Count -gt 0) {
        Invoke-Checked 'dotnet' @('run', '--project', 'tests/MiniInfos.UiCheck', '-c', 'Release', '--', '--suite', ($selection.Suites -join ','), '--quiet')
    }
    if ($selection.Mod) {
        Write-Host 'Mod alterado: execute claude plugin test mod no ambiente do Claude Code.'
    }
}
finally { Pop-Location }

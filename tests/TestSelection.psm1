Set-StrictMode -Version Latest

function Get-TestSelection {
    [CmdletBinding()]
    param([string[]]$Paths = @(), [string[]]$Domains = @(), [switch]$All)

    $catalogue = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'domains.psd1')
    $selected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $full = [bool]$All
    $mod = $false
    $unmapped = @()
    foreach ($domain in $Domains) {
        if (-not $catalogue.Domains.ContainsKey($domain)) { throw "Domínio desconhecido: $domain" }
        [void]$selected.Add($domain)
    }
    foreach ($path in $Paths) {
        $normal = $path.Replace('\', '/') -replace '^\./', ''
        $rule = $catalogue.Rules | Where-Object { $normal -like $_.Path } | Select-Object -First 1
        if ($null -eq $rule) { $full = $true; $unmapped += $normal; continue }
        if ($rule.ContainsKey('Ignore')) { continue }
        if ($rule.ContainsKey('All')) { $full = $true }
        if ($rule.ContainsKey('Mod')) { $mod = $true }
        if ($rule.ContainsKey('Domains')) {
            foreach ($domain in $rule.Domains) {
                if (-not $catalogue.Domains.ContainsKey($domain)) { throw "Regra inválida para $normal`: $domain" }
                [void]$selected.Add($domain)
            }
        }
    }
    # Full .NET regression does not opt independent applications into unrelated checks.
    if ($full) {
        foreach ($domain in $catalogue.Domains.Keys) {
            if (-not $catalogue.Domains[$domain].ContainsKey('Check')) { [void]$selected.Add($domain) }
        }
    }
    $names = @($selected | Sort-Object)
    $filters = @($names | ForEach-Object { $catalogue.Domains[$_].Filter } | Where-Object { $_ } | Sort-Object -Unique)
    $suites = @($names | ForEach-Object { $catalogue.Domains[$_].Suite } | Where-Object { $_ } | Sort-Object -Unique)
    [pscustomobject]@{
        Domains = $names
        All = $full
        Filter = if ($full) { '' } else { $filters -join '|' }
        Suites = if ($full) { @('All') } else { $suites }
        Mod = $mod
        Checks = @($names | ForEach-Object {
            $definition = $catalogue.Domains[$_]
            if ($definition.ContainsKey('Check')) { $definition.Check }
        })
        Unmapped = $unmapped
    }
}

function Get-ChangedTestPaths {
    [CmdletBinding()]
    param([string]$Base = 'HEAD')

    # --no-renames keeps both old and new paths, so moving a file also tests its former consumers.
    $paths = @(& git diff --name-only --no-renames $Base --)
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível comparar com $Base" }
    $paths += @(& git ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível listar os arquivos novos' }
    @($paths | Where-Object { $_ } | Sort-Object -Unique)
}

Export-ModuleMember -Function Get-TestSelection, Get-ChangedTestPaths

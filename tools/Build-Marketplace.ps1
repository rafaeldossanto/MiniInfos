param(
    [Parameter(Mandatory)][string]$Output,
    [string]$Base,
    [string]$Previous
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
$project = Join-Path $repository 'tools/MiniInfos.Catalog/MiniInfos.Catalog.csproj'
$packages = Join-Path $repository 'marketplace/packages'
$temporary = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('MiniInfos.CatalogChanged-' + [guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $temporary | Out-Null
function Invoke-Catalog([string[]]$Arguments) {
    & dotnet run --project $project -c Release --no-build -- @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'A validação ou geração do catálogo falhou.' }
}
try {
    $changed = @()
    if ($Base -and $Base -notmatch '^0+$') {
        $paths = @(& git -C $repository diff --name-only --no-renames $Base HEAD -- marketplace/packages)
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível obter os pacotes alterados.' }
        $changed = @($paths | ForEach-Object { if ($_ -match '^marketplace/packages/([^/]+)/') { $Matches[1] } } | Sort-Object -Unique)
    } else { $changed = @(Get-ChildItem -LiteralPath $packages -Directory | ForEach-Object Name) }
    $changedSources = Join-Path $temporary 'packages'
    New-Item -ItemType Directory -Path $changedSources | Out-Null
    foreach ($id in $changed) {
        $source = Join-Path $packages $id
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { continue }
        Invoke-Catalog @('validate', $source)
        Copy-Item -LiteralPath $source -Destination (Join-Path $changedSources $id) -Recurse
    }
    $arguments = @('build', $changedSources, (Join-Path $temporary 'changed-output'))
    if ($Previous) { $arguments += @('--previous', $Previous) }
    Invoke-Catalog $arguments
    # The full build validates global ID uniqueness and all packages; unchanged packages do not need version bumps.
    Invoke-Catalog @('build', $packages, [IO.Path]::GetFullPath($Output))
}
finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($temporary) -ne $tempRoot -or -not [IO.Path]::GetFileName($temporary).StartsWith('MiniInfos.CatalogChanged-')) {
        throw 'Pasta temporária fora do destino autorizado.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}

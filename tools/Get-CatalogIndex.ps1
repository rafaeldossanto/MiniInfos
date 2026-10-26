param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
if (-not $env:GH_REPO) { throw 'Informe GH_REPO para consultar a publicação do catálogo.' }
$previousOutput = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path (Split-Path $previousOutput) -Force | Out-Null
$errorFile = [IO.Path]::GetTempFileName()
try {
    $metadata = & gh api "repos/$env:GH_REPO/releases/tags/catalog" 2> $errorFile
    if ($LASTEXITCODE -ne 0) {
        if ((Get-Content -LiteralPath $errorFile -Raw) -match 'HTTP 404') {
            Write-Host 'Primeira publicação: ainda não existe uma release catalog.'
            exit 0
        }
        throw 'Não foi possível consultar a release catalog.'
    }
    $release = ($metadata -join "`n") | ConvertFrom-Json
    if (-not @($release.assets | Where-Object name -eq 'index.json').Count) { throw 'A release catalog existe, mas não contém index.json.' }
    & gh release download catalog --repo $env:GH_REPO --pattern index.json --dir (Split-Path $previousOutput) --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível baixar o índice anterior.' }
    if ((Split-Path $previousOutput -Leaf) -ne 'index.json') { Move-Item -LiteralPath (Join-Path (Split-Path $previousOutput) 'index.json') -Destination $previousOutput -Force }
}
finally { Remove-Item -LiteralPath $errorFile -Force }

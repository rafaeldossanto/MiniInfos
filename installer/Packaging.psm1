Set-StrictMode -Version Latest

function Assert-InstallerContents {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Output, [Parameter(Mandatory)][string]$Repository)

    $outputRoot = [IO.Path]::GetFullPath($Output)
    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container)) { throw 'A pasta de publicação do instalador não existe.' }
    $forbiddenSources = @{}
    foreach ($folder in @('marketplace', 'tools', 'front')) {
        $source = Join-Path $Repository $folder
        if (-not (Test-Path -LiteralPath $source)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
            if (-not $forbiddenSources.ContainsKey($file.Name)) { $forbiddenSources[$file.Name] = @() }
            $forbiddenSources[$file.Name] += $file.FullName
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath $outputRoot -File -Recurse) {
        $relative = $file.FullName.Substring($outputRoot.Length + 1).Replace('\', '/')
        if ($relative -notmatch '^(app|claude-plugin)/' -or $relative -match '(^|/)(marketplace|tools|front)/' -or
            $file.Name -like 'MiniInfos.Catalog*' -or $file.Extension -eq '.miniinfos' -or
            ($relative.StartsWith('app/') -and $file.Extension -in @('.cs', '.csproj', '.ps1', '.md', '.png'))) {
            throw "Conteúdo fora do aplicativo e do mod na publicação: $relative"
        }
        if ($forbiddenSources.ContainsKey($file.Name)) {
            $digest = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            foreach ($source in $forbiddenSources[$file.Name]) {
                if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -eq $digest) {
                    throw "Arquivo de marketplace/, tools/ ou front/ encontrado no instalador: $relative"
                }
            }
        }
    }
}

Export-ModuleMember -Function Assert-InstallerContents

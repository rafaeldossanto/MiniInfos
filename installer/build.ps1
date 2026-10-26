# Builds dist\MiniInfos-Setup.exe: the app with .NET inside it (no runtime to install) and the Claude Code mod.
#
# Optional environment:
#   MINIINFOS_PUBLISHER  the name shown as publisher (installer, "Adicionar ou remover programas", the mod's author);
#                        left unset, "MiniInfos".
param(
    [string]$InnoCompiler = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$out = Join-Path $PSScriptRoot 'out'
$publisher = if ($env:MINIINFOS_PUBLISHER) { $env:MINIINFOS_PUBLISHER } else { 'MiniInfos' }
Import-Module (Join-Path $PSScriptRoot 'Packaging.psm1') -Force

# The one version: the project file's, which the app also reports.
$version = ([xml](Get-Content "$root\widget\MiniInfos.csproj" -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'widget\MiniInfos.csproj has no <Version>' }

$out = [IO.Path]::GetFullPath($out)
$expectedOut = [IO.Path]::GetFullPath((Join-Path $root 'installer/out'))
if ($out -ne $expectedOut -or -not $out.StartsWith([IO.Path]::GetFullPath($PSScriptRoot) + [IO.Path]::DirectorySeparatorChar)) {
    throw 'O destino de publicação não está dentro da pasta do instalador.'
}
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }

dotnet publish "$root\widget" -c Release -r win-x64 --self-contained true -o "$out\app" -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# The same marketplace as the repository root: .claude-plugin\marketplace.json naming ./mod.
New-Item -ItemType Directory -Force "$out\claude-plugin\.claude-plugin", "$out\claude-plugin\mod" | Out-Null
Copy-Item "$root\.claude-plugin\marketplace.json" "$out\claude-plugin\.claude-plugin\"
Copy-Item "$root\mod\.claude-plugin", "$root\mod\hooks" "$out\claude-plugin\mod" -Recurse

# The publisher's name goes in the shipped copies only, never in the repository's files.
foreach ($manifest in @(
    @{ Path = "$out\claude-plugin\.claude-plugin\marketplace.json"; Field = 'owner' },
    @{ Path = "$out\claude-plugin\mod\.claude-plugin\plugin.json"; Field = 'author' })) {
    $json = Get-Content $manifest.Path -Raw | ConvertFrom-Json
    $json.($manifest.Field).name = $publisher
    [IO.File]::WriteAllText($manifest.Path, ($json | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding $false))
}

Assert-InstallerContents -Output $out -Repository $root

& $InnoCompiler "/DAppPublisher=$publisher" "/DAppVersion=$version" "$PSScriptRoot\MiniInfos.iss"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Source, [Parameter(Mandatory = $true)][string]$Output)

$ErrorActionPreference = 'Stop'
$sourceDirectory = (Resolve-Path -LiteralPath $Source).Path
$destination = [IO.Path]::GetFullPath($Output)
if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory 'manifest.json') -PathType Leaf)) { throw 'A pasta precisa conter manifest.json na raiz.' }
if ($destination.StartsWith($sourceDirectory.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Escolha um destino fora da pasta de origem.' }
if (Test-Path -LiteralPath $destination) { throw 'O arquivo de destino já existe. Escolha outro nome.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($sourceDirectory, $destination, [IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host "Pacote criado: $destination"

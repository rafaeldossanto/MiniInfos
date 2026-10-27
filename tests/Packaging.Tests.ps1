$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
Import-Module (Join-Path $repository 'installer/Packaging.psm1') -Force
$temporary = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('MiniInfos.Packaging-' + [guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $output = Join-Path $temporary 'out'
    New-Item -ItemType Directory -Path (Join-Path $output 'app'), (Join-Path $output 'claude-plugin/mod') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $output 'app/MiniInfos.dll'), 'app fixture')
    Assert-InstallerContents -Output $output -Repository $repository
    foreach ($name in @('app/MiniInfos.Catalog.dll', 'app/example.miniinfos', 'marketplace/packages/example.json', 'tools/Program.cs', 'front/index.html')) {
        $path = Join-Path $output $name
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        [IO.File]::WriteAllText($path, 'unexpected')
        $rejected = $false
        try { Assert-InstallerContents -Output $output -Repository $repository } catch { $rejected = $true }
        if (-not $rejected) { throw "A checagem aceitou $name no instalador." }
        Remove-Item -LiteralPath $path
    }
    Copy-Item -LiteralPath (Join-Path $repository 'marketplace/examples/ocean/manifest.json') -Destination (Join-Path $output 'app/manifest.json')
    $rejected = $false
    try { Assert-InstallerContents -Output $output -Repository $repository } catch { $rejected = $true }
    if (-not $rejected) { throw 'A checagem aceitou um manifesto copiado do marketplace.' }

    Remove-Item -LiteralPath (Join-Path $output 'app/manifest.json')

    # A copy is judged by where else its bytes live: the app's own font, also shipped by the front, stays; a front-only file does not.
    $shared = Join-Path $temporary 'shared'
    foreach ($copy in @('widget/Assets/Fonts/OFL.txt', 'front/public/fonts/OFL.txt')) {
        New-Item -ItemType Directory -Path (Split-Path (Join-Path $shared $copy)) -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $shared $copy), 'SIL Open Font License')
    }
    [IO.File]::WriteAllText((Join-Path $shared 'front/public/fonts/Site.txt'), 'front only')
    New-Item -ItemType Directory -Path (Join-Path $output 'app/Assets/Fonts') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $shared 'widget/Assets/Fonts/OFL.txt') -Destination (Join-Path $output 'app/Assets/Fonts/OFL.txt')
    Assert-InstallerContents -Output $output -Repository $shared
    Copy-Item -LiteralPath (Join-Path $shared 'front/public/fonts/Site.txt') -Destination (Join-Path $output 'app/Assets/Fonts/Site.txt')
    $rejected = $false
    try { Assert-InstallerContents -Output $output -Repository $shared } catch { $rejected = $true }
    if (-not $rejected) { throw 'A checagem aceitou um arquivo que só existe no front.' }
    Remove-Item -LiteralPath (Join-Path $output 'app/Assets') -Recurse

    # The publish target checks original paths, including files renamed by Link/RelativePath.
    $target = [Security.SecurityElement]::Escape((Join-Path $repository 'installer/PublicationBoundary.targets'))
    $probe = Join-Path $temporary 'boundary.proj'
    foreach ($source in @('widget/Assets/MiniInfos.ico', 'marketplace/examples/ocean/manifest.json', 'tools/MiniInfos.Catalog/Program.cs', 'front/README.md')) {
        $inputPath = [Security.SecurityElement]::Escape((Join-Path $repository $source))
        $project = '<Project><Import Project="' + $target + '" /><ItemGroup><ResolvedFileToPublish Include="' + $inputPath + '"><RelativePath>renamed.dat</RelativePath></ResolvedFileToPublish></ItemGroup></Project>'
        [IO.File]::WriteAllText($probe, $project)
        $result = & dotnet msbuild $probe -nologo -v:quiet -t:CheckMiniInfosPublicationBoundary 2>&1
        if ($source.StartsWith('widget/')) {
            if ($LASTEXITCODE -ne 0) { throw 'A publicação rejeitou um recurso do app.' }
        } elseif ($LASTEXITCODE -eq 0) { throw "A publicação aceitou um arquivo renomeado de $source" }
    }

    # A temporary tree tests git archive without creating a commit or modifying the real repository's index.
    $archiveRepo = Join-Path $temporary 'archive'
    New-Item -ItemType Directory -Path (Join-Path $archiveRepo 'marketplace/packages/example') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repository '.gitattributes') -Destination (Join-Path $archiveRepo '.gitattributes')
    [IO.File]::WriteAllText((Join-Path $archiveRepo 'marketplace/packages/example/manifest.json'), '{}')
    New-Item -ItemType Directory -Path (Join-Path $archiveRepo 'front') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $archiveRepo 'front/index.html'), '<!doctype html>')
    [IO.File]::WriteAllText((Join-Path $archiveRepo 'app.txt'), 'application sources')
    git -C $archiveRepo init --quiet
    # Its own line endings, whatever the machine's core.autocrlf: Windows PowerShell turns git's CRLF warning on stderr into an error.
    git -C $archiveRepo config core.autocrlf false
    git -C $archiveRepo add -- .gitattributes app.txt marketplace/packages/example/manifest.json front/index.html
    $tree = git -C $archiveRepo write-tree
    $archive = Join-Path $temporary 'source.zip'
    git -C $archiveRepo archive --format=zip --output=$archive $tree
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível gerar o ZIP de teste.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        if ($zip.Entries.FullName -match '^marketplace/packages/.+') { throw 'O ZIP de código inclui pacotes do marketplace.' }
        if ($zip.Entries.FullName -match '^front/.+') { throw 'O ZIP de código inclui a landing page.' }
        if ($zip.Entries.FullName -notcontains 'app.txt') { throw 'O ZIP perdeu o código do app.' }
    } finally { $zip.Dispose() }
    $catalogWorkflow = Get-Content -LiteralPath (Join-Path $repository '.github/workflows/marketplace-publish.yml') -Raw
    $appWorkflow = Get-Content -LiteralPath (Join-Path $repository '.github/workflows/release-app.yml') -Raw
    if ([regex]::Matches($catalogWorkflow, '--prerelease --latest=false').Count -ne 2) { throw 'Criar e editar catalog precisam manter prerelease e latest=false.' }
    # The app's release starts as a draft, out of the update banner's sight until it is reviewed and published as latest.
    $create = [regex]::Match($appWorkflow, 'gh release create [^
]*').Value
    if ($create -notmatch ' --draft ' -or $create -match '--latest' -or $appWorkflow -notmatch 'assets.Count -ne 1') {
        throw 'A release do app precisa nascer como rascunho, sem latest, e só com o instalador.'
    }
    $setup = Get-Content -LiteralPath (Join-Path $repository 'installer/MiniInfos.iss') -Raw
    $languages = [regex]::Match($setup, '(?ms)^\[Languages\]\s*(.*?)^\[').Groups[1].Value
    $names = @([regex]::Matches($languages, 'Name: "([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($names.Count -ne 2 -or $names[0] -ne 'en' -or $names -notcontains 'ptbr' -or $setup -notmatch 'LanguageDetectionMethod=uilanguage') {
        throw 'The installer must detect Windows UI language, with Portuguese and an English fallback.'
    }
    $messages = [regex]::Matches($setup, '(?m)^(en|ptbr)\.(\w+)=')
    $english = @($messages | Where-Object { $_.Groups[1].Value -eq 'en' } | ForEach-Object { $_.Groups[2].Value } | Sort-Object)
    $portuguese = @($messages | Where-Object { $_.Groups[1].Value -eq 'ptbr' } | ForEach-Object { $_.Groups[2].Value } | Sort-Object)
    if ($english.Count -eq 0 -or ($english -join ',') -ne ($portuguese -join ',')) { throw 'Installer custom messages must exist in both languages.' }
    Write-Host 'Empacotamento: app/mod isolados, pacotes fora do ZIP e instalador em dois idiomas.'
}
finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($temporary) -ne $tempRoot -or -not [IO.Path]::GetFileName($temporary).StartsWith('MiniInfos.Packaging-')) {
        throw 'Pasta temporária fora do destino autorizado.'
    }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}

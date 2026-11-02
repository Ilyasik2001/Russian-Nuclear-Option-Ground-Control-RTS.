# Сборка архива для релиза: dist\Rusifikator-NuclearOption-GroundControl-<версия>.zip и его SHA-256 (нужен каталогу NOMM).
# Внутри архива файлы лежат без общей папки: Rusifikator-NuclearOption-GroundControl.dll, translations\, fonts\
# и документы — архив распаковывается в BepInEx\plugins\Rusifikator-NuclearOption-GroundControl.
#
#   powershell -File tools\package.ps1
#   powershell -File tools\package.ps1 -GameDir "E:\Steam\steamapps\common\Nuclear Option"
param(
    [string]$GameDir
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$name = 'Rusifikator-NuclearOption-GroundControl'
$project = Join-Path $root "$name.csproj"
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Version not found in $name.csproj" }

$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

$buildArgs = @('build', $project, '-c', 'Release', "-p:PluginDir=$stage")
if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

foreach ($doc in 'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md') {
    Copy-Item (Join-Path $root $doc) $stage
}

$zip = Join-Path $dist "$name-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# ZipFile.CreateFromDirectory в .NET Framework пишет пути через "\", а по стандарту ZIP нужен "/".
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $name = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}
Remove-Item $stage -Recurse -Force

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"sha256:$hash" | Set-Content "$zip.sha256" -Encoding ascii
"$zip"
"sha256:$hash"

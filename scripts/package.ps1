param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot "artifacts"
$portableRoot = Join-Path $artifactRoot ("MagicMouse-Windows-x64-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$version = "0.1.2"
dotnet publish (Join-Path $projectRoot "src/MagicMouse.Windows.App/MagicMouse.Windows.App.csproj") -c $Configuration -p:Platform=x64 -r win-x64 --self-contained true -o $portableRoot -m:1 -nodeReuse:false --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw "La publicación no se completó." }
foreach ($file in @("scripts/Instalar.cmd", "scripts/LEEME.txt", "LICENSE", "THIRD_PARTY_LICENSES.md")) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $portableRoot
}
$payload = Join-Path $artifactRoot ("Magic Mouse Windows x64 " + $version + ".zip")
# Put the app directly at the root of the portable ZIP and embedded installer payload.
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
[System.IO.Compression.ZipFile]::CreateFromDirectory($portableRoot, $payload)
$setupRoot = Join-Path $artifactRoot ("Setup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
dotnet publish (Join-Path $projectRoot "src/MagicMouse.Setup/MagicMouse.Setup.csproj") -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:PayloadPath=$payload" -o $setupRoot -m:1 -nodeReuse:false --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw "El instalador no se completó." }
$setup = Join-Path $artifactRoot ("Magic Mouse Windows x64 " + $version + " Setup.exe")
Copy-Item -LiteralPath (Join-Path $setupRoot "MagicMouse.Setup.exe") -Destination $setup
Write-Output $payload
Write-Output $setup

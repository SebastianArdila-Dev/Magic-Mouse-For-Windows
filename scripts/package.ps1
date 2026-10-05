param([string]$Configuration = "Debug")
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot "artifacts"
$portableRoot = Join-Path $artifactRoot ("MagicMouse-Windows-x64-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
dotnet publish (Join-Path $projectRoot "src/MagicMouse.Windows.App/MagicMouse.Windows.App.csproj") -c $Configuration -p:Platform=x64 -r win-x64 --self-contained true -o $portableRoot -m:1 -nodeReuse:false --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw "La publicación no se completó." }
Copy-Item -LiteralPath (Join-Path $projectRoot "scripts/Instalar.cmd") -Destination $portableRoot
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $portableRoot
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $portableRoot
Compress-Archive -LiteralPath $portableRoot -DestinationPath ($portableRoot + ".zip")
Write-Output ($portableRoot + ".zip")

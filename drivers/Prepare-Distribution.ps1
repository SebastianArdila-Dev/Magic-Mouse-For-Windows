[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$SignToolPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$signTool = (Resolve-Path -LiteralPath $SignToolPath).Path
$names = @('MagicMouseBridge.inf', 'MagicMouseBridge.sys', 'MagicMouseBridge.cat')
foreach ($name in $names) {
    if (!(Test-Path -LiteralPath (Join-Path $package $name) -PathType Leaf)) { throw "Missing $name" }
}
$inf = Get-Content -LiteralPath (Join-Path $package $names[0]) -Raw
if ($inf -notmatch 'DriverVer=\d+/\d+/\d+,0\.1\.2\.0' -or $inf -notmatch 'CatalogFile=MagicMouseBridge\.cat') {
    throw 'Unexpected package identity or version.'
}
$binary = [IO.File]::ReadAllBytes((Join-Path $package $names[1]))
if ($binary.Length -lt 64 -or $binary[0] -ne 0x4D -or $binary[1] -ne 0x5A) { throw 'Invalid driver image.' }
$pe = [BitConverter]::ToInt32($binary, 60)
if ($pe -lt 64 -or $pe -gt $binary.Length - 24 -or
    [BitConverter]::ToUInt32($binary, $pe) -ne 0x4550 -or
    [BitConverter]::ToUInt16($binary, $pe + 4) -ne 0x8664) { throw 'Driver image must be PE x64.' }
# A generic valid Authenticode signature is insufficient for a kernel driver.
& $signTool verify /kp /v /c (Join-Path $package $names[2]) (Join-Path $package $names[1])
if ($LASTEXITCODE -ne 0) { throw 'Kernel signing policy verification failed. No distribution created.' }
& $signTool verify /pa /v (Join-Path $package $names[2])
if ($LASTEXITCODE -ne 0) { throw 'Catalog signature verification failed. No distribution created.' }
& $signTool verify /pa /v /c (Join-Path $package $names[2]) (Join-Path $package $names[0])
if ($LASTEXITCODE -ne 0) { throw 'INF catalog membership verification failed. No distribution created.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$destination = Join-Path $output 'Magic Mouse Bridge x64 0.1.2.zip'
if (Test-Path -LiteralPath $destination) { throw 'Destination already exists; choose a new output directory.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$files = $names | ForEach-Object { Join-Path $package $_ }
Compress-Archive -LiteralPath $files -DestinationPath $destination
$manifest = [ordered]@{
    Version = '0.1.2'; Architecture = 'x64'; KernelSignatureVerified = $true
    HardwareValidation = 'Required separately before public release'
    Files = @($files | ForEach-Object { [ordered]@{ Name = [IO.Path]::GetFileName($_); SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'package-verification.json') -Encoding utf8
Write-Output $destination

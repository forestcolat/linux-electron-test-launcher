# Builds ReStreet-Viewer.zip: the Windows Electron runtime + the viewer app (resources\app), ready to unzip next to
# FifaStreetLauncher.exe. Run it on Windows (PowerShell 5.1 or 7) with an internet connection:
#
#     powershell -ExecutionPolicy Bypass -File Viewer\build-viewer.ps1
#     powershell -ExecutionPolicy Bypass -File Viewer\build-viewer.ps1 -Version 33.2.0    (a fixed Electron version)
#
# Publish the zip as an OPTIONAL Linux download. Do not put it in the Windows installer: Windows does not use it.
param(
    [string]$Version = "",
    [string]$Out = "ReStreet-Viewer.zip"
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$work = Join-Path ([System.IO.Path]::GetTempPath()) "restreet-viewer-build"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null

if ($Version -eq "") {
    $release = Invoke-RestMethod "https://api.github.com/repos/electron/electron/releases/latest" -Headers @{ "User-Agent" = "restreet-viewer-build" }
    $asset = $release.assets | Where-Object { $_.name -match '^electron-v[\d\.]+-win32-x64\.zip$' } | Select-Object -First 1
    if (-not $asset) { throw "The win32-x64 Electron zip was not found in the latest Electron release." }
    $name = $asset.name
    $link = $asset.browser_download_url
} else {
    $name = "electron-v$Version-win32-x64.zip"
    $link = "https://github.com/electron/electron/releases/download/v$Version/$name"
}

$zip = Join-Path $work $name
Write-Host "Downloading $name ..."
Invoke-WebRequest $link -OutFile $zip -UseBasicParsing

$viewer = Join-Path $work "Viewer"
Expand-Archive $zip -DestinationPath $viewer
Rename-Item (Join-Path $viewer "electron.exe") "ReStreetViewer.exe"
$app = Join-Path $viewer "resources\app"
New-Item -ItemType Directory $app -Force | Out-Null
Copy-Item (Join-Path $here "resources\app\*") $app -Recurse

$dest = Join-Path (Get-Location) $Out
if (Test-Path $dest) { Remove-Item $dest -Force }
# .NET's ZipFile writes forward slashes in the entry names, so the zip also unpacks correctly on Linux
# (PowerShell 5.1's Compress-Archive does not).
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($viewer, $dest, [System.IO.Compression.CompressionLevel]::Optimal, $true)
Write-Host "Done: $dest  ($name)"

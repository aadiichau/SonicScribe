# Build SonicScribe and create GitHub release packages.
param(
    [string]$Version = "v1.0.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$distFolder = Join-Path $projectRoot "dist\SonicScribe"
$releasesFolder = Join-Path $projectRoot "releases"
$portableZip = Join-Path $releasesFolder "SonicScribe-$Version-Portable-win-x64.zip"
$legacyZip = Join-Path $releasesFolder "SonicScribe-win-x64.zip"

Write-Host "Building SonicScribe $Version..." -ForegroundColor Cyan
Stop-Process -Name SonicScribe -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

& (Join-Path $projectRoot "publish.ps1")

if (-not (Test-Path (Join-Path $distFolder "SonicScribe.exe"))) {
    throw "Build failed: SonicScribe.exe not found."
}

$startHere = Join-Path $distFolder "START-HERE.txt"
@(
    "SonicScribe - Portable Edition"
    "=============================="
    ""
    "No installer required. This folder is fully portable."
    ""
    "Quick start:"
    "  1. Keep all files in this folder together"
    "  2. Install Python 3.11/3.12 + faster-whisper (see GitHub README)"
    "  3. Double-click SonicScribe.exe or Start SonicScribe.bat"
    ""
    "Data is saved locally:"
    "  - Transcripts: Documents\SonicScribe\Outputs"
    "  - Settings:    %LocalAppData%\SonicScribe"
    ""
    "GitHub: https://github.com/aadiichau/SonicScribe"
) | Set-Content -Path $startHere -Encoding UTF8

New-Item -ItemType Directory -Force -Path $releasesFolder | Out-Null

foreach ($zipPath in @($portableZip, $legacyZip)) {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
}

Write-Host "Creating portable release zip..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $distFolder "*") -DestinationPath $portableZip -CompressionLevel Optimal
Copy-Item $portableZip $legacyZip -Force

$sizeMb = [math]::Round((Get-Item $portableZip).Length / 1MB, 1)
Write-Host ""
Write-Host "Release packages ready!" -ForegroundColor Green
Write-Host "  Portable: $portableZip ($sizeMb MB)"
Write-Host "  Mirror:   $legacyZip"
Write-Host ""
Write-Host "Run .\publish-release.ps1 -Version $Version to upload to GitHub Releases." -ForegroundColor Yellow
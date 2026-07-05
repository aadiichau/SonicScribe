# Build SonicScribe and create a GitHub Release zip.
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$zipPath = Join-Path $projectRoot "SonicScribe-win-x64.zip"

Write-Host "Building SonicScribe..." -ForegroundColor Cyan
& (Join-Path $projectRoot "publish.ps1")

$distFolder = Join-Path $projectRoot "dist\SonicScribe"
if (-not (Test-Path (Join-Path $distFolder "SonicScribe.exe"))) {
    throw "Build failed: SonicScribe.exe not found."
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating release zip..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $distFolder "*") -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Release package ready!" -ForegroundColor Green
Write-Host "  $zipPath ($sizeMb MB)"
Write-Host ""
Write-Host "Upload to GitHub Releases as SonicScribe-win-x64.zip" -ForegroundColor Yellow
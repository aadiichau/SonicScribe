# Build SonicScribe Windows installer (.exe) with Inno Setup.
param(
    [string]$Version = "v1.0.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$distExe = Join-Path $projectRoot "dist\SonicScribe\SonicScribe.exe"
$issFile = Join-Path $projectRoot "installer\SonicScribe.iss"
$versionNumber = $Version.TrimStart("v", "V")

$innoPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$iscc = $innoPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Host "Inno Setup not found. Installing..." -ForegroundColor Cyan
    winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements | Out-Null
    $iscc = $innoPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $iscc) {
    throw "Inno Setup compiler not found. Install from https://jrsoftware.org/isinfo.php"
}

if (-not (Test-Path $distExe)) {
    Write-Host "App not built yet. Running release.ps1..." -ForegroundColor Cyan
    & (Join-Path $projectRoot "release.ps1") -Version $Version
}

Write-Host "Building installer SonicScribe-Setup-v$versionNumber.exe ..." -ForegroundColor Cyan
& $iscc "/DAppVersion=$versionNumber" $issFile

$setupExe = Join-Path $projectRoot "releases\SonicScribe-Setup-v$versionNumber.exe"
if (-not (Test-Path $setupExe)) {
    throw "Installer build failed."
}

$sizeMb = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
Write-Host ""
Write-Host "Installer ready!" -ForegroundColor Green
Write-Host "  $setupExe ($sizeMb MB)"
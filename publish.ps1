# Build a standalone SonicScribe app you can double-click to run.
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "LocalScribe\LocalScribe.csproj"
$distFolder = Join-Path $projectRoot "dist\SonicScribe"

Write-Host "Publishing SonicScribe (Release, win-x64, self-contained)..." -ForegroundColor Cyan

dotnet publish $projectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishTrimmed=false `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -o $distFolder

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed."
}

$exePath = Join-Path $distFolder "SonicScribe.exe"
$desktopShortcut = Join-Path $env:USERPROFILE "Desktop\SonicScribe.lnk"
$legacyShortcut = Join-Path $env:USERPROFILE "Desktop\LocalScribe.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($desktopShortcut)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $distFolder
$shortcut.Description = "SonicScribe - Local Whisper transcription"
$iconPath = Join-Path $distFolder "Assets\AppIcon.ico"
if (-not (Test-Path $iconPath)) {
    throw "Missing icon at $iconPath"
}

$shortcut.IconLocation = "$iconPath,0"
$shortcut.Save()

# Also place a copy beside the exe so Windows can resolve the embedded icon resource.
Copy-Item $iconPath (Join-Path $distFolder "SonicScribe.ico") -Force

if (Test-Path $legacyShortcut) {
    Remove-Item $legacyShortcut -Force
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host "  App folder: $distFolder"
Write-Host "  Executable: $exePath"
Write-Host "  Desktop shortcut: $desktopShortcut"
Write-Host ""
Write-Host "Double-click SonicScribe on your desktop to launch." -ForegroundColor Yellow
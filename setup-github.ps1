# One-time setup: push SonicScribe to GitHub and publish a release.
param(
    [Parameter(Mandatory = $true)]
    [string]$GitHubUsername,

    [string]$RepoName = "SonicScribe",
    [string]$Tag = "v1.0.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

$remoteUrl = "https://github.com/$GitHubUsername/$RepoName.git"
$zipPath = Join-Path $projectRoot "SonicScribe-win-x64.zip"

Write-Host ""
Write-Host "SonicScribe GitHub setup" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Before running this script, create an EMPTY repo on GitHub:" -ForegroundColor Yellow
Write-Host "  https://github.com/new" -ForegroundColor White
Write-Host "  Name: $RepoName" -ForegroundColor White
Write-Host "  Visibility: Public (recommended)" -ForegroundColor White
Write-Host "  Do NOT add README, .gitignore, or license (we already have them)" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter after the repo exists on GitHub"

git branch -M main
git remote remove origin 2>$null
git remote add origin $remoteUrl

Write-Host "Pushing code..." -ForegroundColor Cyan
git push -u origin main

if (-not (Test-Path $zipPath)) {
    Write-Host "Building release zip..." -ForegroundColor Cyan
    Stop-Process -Name SonicScribe -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    & (Join-Path $projectRoot "release.ps1")
}

Write-Host ""
Write-Host "Code pushed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Publish the download zip:" -ForegroundColor Yellow
Write-Host "  1. Open https://github.com/$GitHubUsername/$RepoName/releases/new" -ForegroundColor White
Write-Host "  2. Choose tag: $Tag (create new tag)" -ForegroundColor White
Write-Host "  3. Release title: SonicScribe $Tag" -ForegroundColor White
Write-Host "  4. Upload file: $zipPath" -ForegroundColor White
Write-Host "  5. Click Publish release" -ForegroundColor White
Write-Host ""
Write-Host "Your README download link will work after the zip is uploaded." -ForegroundColor Green
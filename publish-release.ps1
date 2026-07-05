# Upload SonicScribe release packages to GitHub Releases.
param(
    [string]$Version = "v1.0.0",
    [string]$Repo = "aadiichau/SonicScribe"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$releasesFolder = Join-Path $projectRoot "releases"
$portableZip = Join-Path $releasesFolder "SonicScribe-$Version-Portable-win-x64.zip"
$legacyZip = Join-Path $releasesFolder "SonicScribe-win-x64.zip"

if (-not (Test-Path $portableZip)) {
    Write-Host "Packages not found. Building..." -ForegroundColor Cyan
    & (Join-Path $projectRoot "release.ps1") -Version $Version
}

function Get-GitHubToken {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "git"
    $psi.Arguments = "credential fill"
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($psi)
    $process.StandardInput.WriteLine("protocol=https")
    $process.StandardInput.WriteLine("host=github.com")
    $process.StandardInput.WriteLine("")
    $process.StandardInput.Close()
    $output = $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()

    foreach ($line in $output -split "`n") {
        if ($line -like "password=*") {
            return $line.Substring("password=".Length).Trim()
        }
    }

    return $null
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
    $authStatus = & gh auth status 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Publishing release with GitHub CLI..." -ForegroundColor Cyan
        $notes = @"
## SonicScribe $Version

### Portable download (recommended)
- **SonicScribe-$Version-Portable-win-x64.zip** — unzip anywhere and run. No installer.
- Double-click ``SonicScribe.exe`` or ``Start SonicScribe.bat``.

### Requirements
- Windows 10/11 (64-bit)
- Python 3.11/3.12 + faster-whisper + PyTorch
- FFmpeg recommended for video

See the [README](https://github.com/$Repo#readme) for setup commands.
"@

        & gh release view $Version -R $Repo 2>$null
        if ($LASTEXITCODE -eq 0) {
            & gh release upload $Version $portableZip $legacyZip -R $Repo --clobber
        }
        else {
            & gh release create $Version `
                --repo $Repo `
                --title "SonicScribe $Version" `
                --notes $notes `
                $portableZip `
                $legacyZip
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "Release published!" -ForegroundColor Green
            Write-Host "  https://github.com/$Repo/releases/tag/$Version"
            exit 0
        }
    }
}

$token = Get-GitHubToken
if (-not $token) {
    Write-Host ""
    Write-Host "GitHub authentication required. Run ONE of these:" -ForegroundColor Yellow
    Write-Host "  gh auth login" -ForegroundColor White
    Write-Host "  Then re-run: .\publish-release.ps1 -Version $Version" -ForegroundColor White
    Write-Host ""
    Write-Host "Or upload manually:" -ForegroundColor Yellow
    Write-Host "  https://github.com/$Repo/releases/new?tag=$Version" -ForegroundColor White
    Write-Host "  Upload: $portableZip" -ForegroundColor White
    exit 1
}

Write-Host "Publishing release via GitHub API..." -ForegroundColor Cyan

$headers = @{
    Authorization = "Bearer $token"
    Accept = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$releaseBody = @{
    tag_name = $Version
    name = "SonicScribe $Version"
    body = "Portable Windows build. Unzip and run SonicScribe.exe. Python + faster-whisper required."
    draft = $false
    prerelease = $false
} | ConvertTo-Json

try {
    $release = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$Repo/releases" -Headers $headers -Body $releaseBody -ContentType "application/json"
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 422) {
        $release = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/$Repo/releases/tags/$Version" -Headers $headers
    }
    else {
        throw
    }
}

function Upload-Asset($filePath) {
    $fileName = [Uri]::EscapeDataString([IO.Path]::GetFileName($filePath))
    $uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$($release.id)/assets?name=$fileName"
    $bytes = [IO.File]::ReadAllBytes($filePath)
    Invoke-RestMethod -Method Post -Uri $uploadUrl -Headers @{
        Authorization = "Bearer $token"
        Accept = "application/vnd.github+json"
    } -Body $bytes -ContentType "application/zip" | Out-Null
    Write-Host "  Uploaded $([IO.Path]::GetFileName($filePath))" -ForegroundColor Green
}

Upload-Asset $portableZip
Upload-Asset $legacyZip

Write-Host ""
Write-Host "Release published!" -ForegroundColor Green
Write-Host "  $($release.html_url)"
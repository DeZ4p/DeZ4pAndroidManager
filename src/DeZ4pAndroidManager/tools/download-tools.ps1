# ===============================================================
# DeZ4p Android Manager — Download bundled tools
# (c) DeZ4p | t.me/DeZ4p | All Rights Reserved
# ===============================================================
#
# Downloads:
#   - Android Platform Tools (adb + fastboot) from Google
#   - scrcpy (screen mirror) from Genymobile
#
# Usage:
#   .\download-tools.ps1
#   .\download-tools.ps1 -Force
#
# ===============================================================

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$toolsDir       = $PSScriptRoot
$platformTools  = Join-Path $toolsDir "platform-tools"
$scrcpyDir      = Join-Path $toolsDir "scrcpy"
$tempDir        = Join-Path $env:TEMP "dezp-tools-download"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "==============================================================" -ForegroundColor Magenta
    Write-Host "  $Text" -ForegroundColor Magenta
    Write-Host "==============================================================" -ForegroundColor Magenta
    Write-Host ""
}

function Write-Step { param([string]$Text) Write-Host "  [..] $Text" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Text) Write-Host "  [OK] $Text" -ForegroundColor Green }
function Write-Warn { param([string]$Text) Write-Host "  [!!] $Text" -ForegroundColor Yellow }
function Write-Err  { param([string]$Text) Write-Host "  [XX] $Text" -ForegroundColor Red }

Write-Header "DeZ4p Android Manager - Download Tools"

# ─── Check for existing ───
$adbPath = Join-Path $platformTools "adb.exe"
$scrcpyExe = Join-Path $scrcpyDir "scrcpy.exe"

if ((Test-Path $adbPath) -and (Test-Path $scrcpyExe) -and -not $Force) {
    Write-Ok "Tools already present"
    Write-Host "       - $adbPath" -ForegroundColor Gray
    Write-Host "       - $scrcpyExe" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Run with -Force to re-download." -ForegroundColor Yellow
    Write-Host ""
    exit 0
}

# ─── Clean temp ───
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

# ─── Step 1: Platform Tools ───
Write-Step "Downloading Android Platform Tools..."

$ptUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip"
$ptZip = Join-Path $tempDir "platform-tools.zip"

try {
    Invoke-WebRequest -Uri $ptUrl -OutFile $ptZip -UseBasicParsing
    Write-Ok "Downloaded platform-tools.zip"
} catch {
    Write-Err "Failed to download platform-tools: $_"
    exit 1
}

Write-Step "Extracting to $platformTools..."
if (Test-Path $platformTools) { Remove-Item $platformTools -Recurse -Force }
Expand-Archive -Path $ptZip -DestinationPath $toolsDir -Force

if (Test-Path $adbPath) {
    $ver = (& $adbPath version 2>&1 | Select-Object -First 1)
    Write-Ok "adb installed: $ver"
} else {
    Write-Err "adb.exe not found after extract"
    exit 1
}

# ─── Step 2: scrcpy ───
Write-Step "Fetching latest scrcpy release info..."

try {
    $api = "https://api.github.com/repos/Genymobile/scrcpy/releases/latest"
    $release = Invoke-RestMethod -Uri $api -Headers @{ "User-Agent" = "DeZ4p-Downloader" }
    $asset = $release.assets | Where-Object { $_.name -like "scrcpy-win64-v*.zip" } | Select-Object -First 1

    if (-not $asset) {
        Write-Warn "No win64 asset found in latest release"
        Write-Warn "Skipping scrcpy — you can download manually from: https://github.com/Genymobile/scrcpy/releases"
    } else {
        Write-Step "Downloading $($asset.name)..."
        $scrcpyZip = Join-Path $tempDir "scrcpy.zip"
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $scrcpyZip -UseBasicParsing
        Write-Ok "Downloaded $($asset.name)"

        Write-Step "Extracting scrcpy..."
        $extractDir = Join-Path $tempDir "scrcpy-extract"
        Expand-Archive -Path $scrcpyZip -DestinationPath $extractDir -Force

        # Move files from inner folder to scrcpy/
        if (Test-Path $scrcpyDir) { Remove-Item $scrcpyDir -Recurse -Force }
        $innerDir = Get-ChildItem $extractDir -Directory | Select-Object -First 1
        if ($innerDir) {
            Move-Item $innerDir.FullName $scrcpyDir
        } else {
            Move-Item $extractDir $scrcpyDir
        }

        if (Test-Path $scrcpyExe) {
            Write-Ok "scrcpy installed"
        } else {
            Write-Warn "scrcpy.exe not found after extract"
        }
    }
} catch {
    Write-Warn "Failed to fetch scrcpy: $_"
    Write-Warn "You can download manually from: https://github.com/Genymobile/scrcpy/releases"
}

# ─── Cleanup ───
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

# ─── Summary ───
Write-Header "Done!"

Write-Host "  Installed tools:" -ForegroundColor Cyan

if (Test-Path $adbPath) {
    $size = [math]::Round((Get-Item $adbPath).Length / 1MB, 2)
    Write-Host "    [OK] adb.exe      ($size MB)" -ForegroundColor Green
}
$fbPath = Join-Path $platformTools "fastboot.exe"
if (Test-Path $fbPath) {
    $size = [math]::Round((Get-Item $fbPath).Length / 1MB, 2)
    Write-Host "    [OK] fastboot.exe ($size MB)" -ForegroundColor Green
}
if (Test-Path $scrcpyExe) {
    $size = [math]::Round((Get-Item $scrcpyExe).Length / 1MB, 2)
    Write-Host "    [OK] scrcpy.exe   ($size MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "  You can now build the project:" -ForegroundColor Cyan
Write-Host "    dotnet build" -ForegroundColor Gray
Write-Host ""
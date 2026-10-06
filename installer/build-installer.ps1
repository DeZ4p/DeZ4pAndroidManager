# ===============================================================
# DeZ4p Android Manager - Installer Builder (x64)
# (c) DeZ4p | t.me/DeZ4p | All Rights Reserved
# ===============================================================

param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

# Paths
$root         = Split-Path $PSScriptRoot -Parent
$csproj       = Join-Path $root "src\DeZ4pAndroidManager\DeZ4pAndroidManager.csproj"
$srcTools     = Join-Path $root "src\DeZ4pAndroidManager\tools"
$portableDir  = Join-Path $root "artifacts\portable-win-x64"
$installerDir = $PSScriptRoot
$issFile      = Join-Path $installerDir "DeZ4pAndroidManager.iss"
$outputDir    = Join-Path $installerDir "Output"

# Find Inno Setup compiler
$candidates = @(
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)
$iscc = $null
foreach ($p in $candidates) {
    if (Test-Path $p) { $iscc = $p; break }
}
if (-not $iscc) {
    Write-Host ""
    Write-Host "  [ERR] ISCC.exe not found! Install Inno Setup 6 or 7." -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Magenta
Write-Host "  DeZ4p Android Manager - Installer Builder" -ForegroundColor Magenta
Write-Host "  (c) DeZ4p | t.me/DeZ4p" -ForegroundColor Magenta
Write-Host "==============================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  Inno Setup : $iscc" -ForegroundColor Gray
Write-Host "  Project    : $root" -ForegroundColor Gray
Write-Host "  Output     : $outputDir" -ForegroundColor Gray
Write-Host ""

# STEP 1 - Cleanup
Write-Host "--- Step 1: Cleanup ---" -ForegroundColor Cyan
Get-Process adb -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process DeZ4pAndroidManager -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Write-Host "  [OK] Processes stopped" -ForegroundColor Green
Write-Host ""

# STEP 2 - Portable publish
if (-not $SkipPublish) {
    Write-Host "--- Step 2: Portable Publish (x64) ---" -ForegroundColor Cyan
    if (Test-Path $portableDir) {
        try { Remove-Item $portableDir -Recurse -Force -ErrorAction Stop }
        catch { Write-Host "  [!] Partial clean failed, continuing" -ForegroundColor Yellow }
    }
    New-Item -ItemType Directory -Force -Path $portableDir | Out-Null

    Write-Host "  [i] dotnet publish..." -ForegroundColor Gray
    $pubArgs = @(
        "publish", $csproj,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=embedded",
        "-p:PublishReadyToRun=false",
        "-o", $portableDir
    )
    & dotnet @pubArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "  [ERR] dotnet publish failed!" -ForegroundColor Red
        Write-Host ""
        exit 1
    }
    Write-Host "  [OK] exe published" -ForegroundColor Green

    # Copy tools
    Write-Host "  [i] Copying tools..." -ForegroundColor Gray
    $destTools = Join-Path $portableDir "tools"
    $null = robocopy $srcTools $destTools /E /NFL /NDL /NJH /NJS /NC /NS
    if ($LASTEXITCODE -ge 8) {
        Write-Host ""
        Write-Host "  [ERR] robocopy failed!" -ForegroundColor Red
        Write-Host ""
        exit 1
    }
    Write-Host "  [OK] tools copied" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "--- Step 2: Skipped (reusing portable) ---" -ForegroundColor DarkGray
    Write-Host ""
}

# Verify portable
$exePath = Join-Path $portableDir "DeZ4pAndroidManager.exe"
$adbPath = Join-Path $portableDir "tools\platform-tools\adb.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "  [ERR] Main exe not found: $exePath" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $adbPath)) {
    Write-Host "  [ERR] adb.exe not found: $adbPath" -ForegroundColor Red
    exit 1
}
$exeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host "  Portable ready: $exeMB MB" -ForegroundColor Gray
Write-Host ""

# STEP 3 - Clean output
Write-Host "--- Step 3: Clean old Output ---" -ForegroundColor Cyan
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
Write-Host "  [OK] Output folder ready" -ForegroundColor Green
Write-Host ""

# STEP 4 - Compile installer
Write-Host "--- Step 4: Compile Installer ---" -ForegroundColor Cyan
Write-Host "  [i] Running ISCC.exe..." -ForegroundColor Gray
Write-Host ""

$sw = [System.Diagnostics.Stopwatch]::StartNew()
& $iscc $issFile
$sw.Stop()
$elapsed = [math]::Round($sw.Elapsed.TotalSeconds, 1)

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  [ERR] Inno Setup compile failed (exit $LASTEXITCODE)!" -ForegroundColor Red
    Write-Host ""
    exit 1
}
Write-Host ""
Write-Host "  [OK] Installer compiled in $elapsed seconds" -ForegroundColor Green
Write-Host ""

# STEP 5 - Result
Write-Host "--- Result ---" -ForegroundColor Cyan
$installers = Get-ChildItem $outputDir -Filter "*.exe" -ErrorAction SilentlyContinue
if ($installers) {
    foreach ($file in $installers) {
        $sizeMB = [math]::Round($file.Length / 1MB, 2)
        Write-Host "  [OK] $($file.Name)" -ForegroundColor Green
        Write-Host "       Size : $sizeMB MB" -ForegroundColor Gray
        Write-Host "       Path : $($file.FullName)" -ForegroundColor Gray
        Write-Host "       Built: $($file.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    }
} else {
    Write-Host "  [ERR] No installer found in output!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Magenta
Write-Host "  Installer build complete!" -ForegroundColor Magenta
Write-Host "==============================================================" -ForegroundColor Magenta
Write-Host ""
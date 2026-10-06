<!-- (c) DeZ4p | t.me/DeZ4p | All Rights Reserved -->

**Language / Ø²Ø¨Ø§Ù†:** &nbsp;
[English](README.md) &nbsp;|&nbsp; [ÙØ§Ø±Ø³ÛŒ](README.fa.md)

---
<div align="center">

# DeZ4p Android Manager

**The complete Windows toolkit for Android device management.**

**ADB - Fastboot - Flash - Monitor - File Manager - Everything.**

[![Build](https://github.com/DeZ4p/DeZ4pAndroidManager/actions/workflows/build.yml/badge.svg)](https://github.com/DeZ4p/DeZ4pAndroidManager/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/DeZ4p/DeZ4pAndroidManager?include_prereleases&color=4F8CFF)](https://github.com/DeZ4p/DeZ4pAndroidManager/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6)](#system-requirements)
[![Android](https://img.shields.io/badge/Android-7%20%E2%86%92%2016-3DDC84)](#system-requirements)

**Developed by [DeZ4p](https://t.me/DeZ4p) - t.me/DeZ4p**

</div>

---

## Table of Contents

- [Screenshots](#screenshots)
- [What is this?](#what-is-this)
- [Features](#features)
- [Download](#download)
- [First Use](#first-use)
- [System Requirements](#system-requirements)
- [Tested On](#tested-on)
- [Build from Source](#build-from-source)
- [Project Structure](#project-structure)
- [Tech Stack](#tech-stack)
- [Reporting Bugs](#reporting-bugs)
- [License](#license)
- 

---

## Screenshots

<div align="center">

### Dashboard
Real-time device overview with battery, RAM, storage, CPU, network, and health score.

![Dashboard](docs/screenshots/01-dashboard.png)

### Device Info
Complete hardware and software information — 60+ fields.

![Device Info](docs/screenshots/02-device-info.png)

### File Manager
Browse, download, upload, and manage files on the device.

![File Manager](docs/screenshots/03-file-manager.png)

### Apps Manager
List installed apps, get APK info, uninstall.

![Apps Manager](docs/screenshots/04-apps-manager.png)

### APK Installer
Install single `.apk` files or split `.apks` packages.

![APK Installer](docs/screenshots/05-apk-installer.png)

### Flash Tools
Flash boot, recovery, system, and vendor partitions.

![Flash Tools](docs/screenshots/06-flash-tools.png)

### ADB Console
Interactive `adb shell` with output capture.

![ADB Console](docs/screenshots/07-adb-console.png)

### Logcat Viewer
Real-time device logs with filter.

![Logcat Viewer](docs/screenshots/08-logcat-viewer.png)

### Settings
Themes, language, and application preferences.

![Settings](docs/screenshots/09-settings.png)

### Battery Lab
Cycle count, design capacity, current capacity, health, age.

![Battery Lab](docs/screenshots/10-battery-lab.png)

</div>
---

## What is this?

**DeZ4p Android Manager** is a professional Windows desktop application that gives you **complete control over any Android device** â€” from a single clean interface.

It wraps every day-to-day Android power-user task into one app:

- **Real-time monitoring** of battery, RAM, CPU, storage, network
- **Full file management** (browse, edit, upload, download, copy, rename)
- **APK installation** (single `.apk` + split `.apks`)
- **App management** (list, backup, uninstall)
- **Bootloader / Fastboot / Recovery operations**
- **Partition management** and backup/restore
- **Screen mirroring** with bundled scrcpy
- **Logcat, shell console, process manager, sensors panel**
- **Security center** (SELinux, root, verified boot)
- **Wireless ADB** (Wi-Fi pairing without USB)

And much more â€” all **bilingual (English + Persian)** with **full RTL support**, **Dark & Light themes**, and **zero runtime dependencies** for the end user.

---

## Features

### ðŸ“Š Monitoring & Info

- **Live Dashboard** â€” Real-time battery level, health, temperature, voltage, storage, RAM, CPU load, network speed, uptime
- **Health Score** â€” Composite device health score based on multiple factors
- **Top Apps** â€” Live list of apps by CPU and RAM usage
- **Battery Lab** â€” Cycle count, design capacity, current capacity, health %, age
- **Thermal Zones** â€” CPU, GPU, battery, skin temperature sensors
- **Storage Breakdown** â€” Per-category breakdown of device storage
- **Sensors Panel** â€” All hardware sensors with live values
- **Device Info** â€” 60+ fields: brand, model, codename, Android version, SDK, security patch, CPU, GPU, RAM, storage, resolution, density, refresh rate, WiFi, Bluetooth, SIM, baseband, kernel, bootloader, root, SELinux, verified boot

### ðŸ“ File & App Management

- **File Manager** â€” Browse, download, upload, rename, delete, create folders, multi-select
- **Copy / Cut / Paste** on device
- **APK Installer** â€” Single `.apk` + split `.apks` (auto-detect via `install-multiple`)
- **Apps Manager** â€” List installed apps, get APK info, uninstall
- **APK Backup** â€” Backup APKs from device to PC
- **Split APK Tools** â€” Extract, analyze, re-sign split APKs
- **Backup & Restore** â€” Backup / restore apps and data
- **Media Gallery** â€” Browse photos, videos, audio
- **Contacts & SMS** â€” Read and backup contacts and messages

### ðŸ”¥ Flash & Bootloader

- **Flash Tools** â€” Flash boot, recovery, system, vendor partitions
- **Bootloader Tools** â€” Unlock/lock, getvar info, OEM commands
- **Recovery Manager** â€” Reboot to recovery, sideload, ADB in recovery
- **Partition Tools** â€” List all partitions, sizes, types, backup individual partitions
- **Reboot Options** â€” Normal, Recovery, Bootloader, Fastbootd, EDL, Shutdown, SystemUI restart

### ðŸ›  Advanced

- **ADB Console** â€” Interactive `adb shell` with output capture
- **Fastboot Console** â€” Interactive `fastboot` command runner
- **Logcat Viewer** â€” Real-time device logs with filter
- **Process Manager** â€” Running processes with CPU, RAM, PID
- **Network Tools** â€” WiFi, mobile network info, signal strength
- **Script Runner** â€” Run ADB/Shell scripts
- **Task Scheduler** â€” Schedule automated ADB tasks

### ðŸ›¡ Security & Privacy

- **Security Center** â€” SELinux status, root detection, verified boot state
- **Permissions Manager** â€” View app permissions
- **Privacy Tools** â€” Privacy audit utilities
- **Wireless ADB** â€” Pair over Wi-Fi without USB (Android 11+)

### ðŸŽ¥ Media

- **Screen Mirror** â€” Powered by bundled **scrcpy v5.0**
- **Screenshot** â€” One-click device screenshot
- **Screen Record** â€” Record device screen to MP4

### ðŸŽ¨ UI / UX

- **Bilingual** â€” English + Persian with full RTL support
- **Dark & Light themes** â€” Auto-detect from Windows, manual toggle
- **Smooth animations** â€” 60 FPS transitions
- **DPI aware** â€” Crisp on 100%, 125%, 150%, 200% scaling
- **Device state gating** â€” Features auto-lock when mode incompatible
- **Hot-plug** â€” Automatically detects device connect/disconnect
- **Activity Log** â€” Persistent event history (last 500 actions)

### âš¡ Performance

- **Self-contained** â€” No .NET runtime needed
- **Single-file build** â€” One `.exe` to run
- **Offline** â€” Works fully without internet
- **Fast startup** â€” Splash â†’ main window in under 2 seconds

---

## Download

Get the latest version from the [**Releases page**](https://github.com/DeZ4p/DeZ4pAndroidManager/releases/latest).

| File | Description | Size |
|------|-------------|------|
| `DeZ4pAndroidManager-Setup-x64-1.0.0.exe` | **Installer** â€” recommended | ~95 MB |
| `DeZ4pAndroidManager-Portable-x64-1.0.0.zip` | **Portable** â€” no install | ~103 MB |

**Why so big?** The app is fully self-contained â€” it includes the .NET 8 runtime (~70 MB) plus bundled `adb`, `fastboot`, and `scrcpy`. **No additional downloads needed.**

---

## First Use

### Step 1 â€” Enable USB Debugging on your Android device

1. Open **Settings** â†’ **About Phone**
2. Tap **Build Number** 7 times (a toast will say "You are now a developer")
3. Go back â†’ **Developer Options** â†’ enable **USB Debugging**

### Step 2 â€” Connect your device

1. Plug your Android device into your PC via USB
2. On the device, a dialog appears: **Allow USB Debugging?** â€” tap **Allow** (check "Always allow" if you want)

### Step 3 â€” Launch DeZ4p Android Manager

1. Run `DeZ4pAndroidManager.exe`
2. The Dashboard opens automatically once your device is detected
3. Use the sidebar to navigate between tools

---

## System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **OS** | Windows 10 (build 17763 / 1809) | Windows 11 latest |
| **Also supported** | Server 2019, Server 2022 | â€” |
| **CPU** | x64 (AMD64) | Any modern x64 |
| **RAM** | 4 GB | 8 GB |
| **Disk** | 250 MB free | 500 MB free |
| **.NET** | *Not required* (self-contained) | â€” |
| **Android device** | Android 7 (API 24) | Android 13â€“16 |
| **ADB version** | 1.0.36 | 1.0.41+ |

---

## Tested On

This app has been tested on real hardware across multiple OEMs:

| Device | OEM | Android | Result |
|--------|-----|---------|--------|
| **Poco X6 Pro** | Xiaomi | HyperOS (Android 14) | âœ… Full |
| **Poco C71** | Xiaomi | MIUI (Android 13) | âœ… Full |
| **Huawei P Smart 2019** | Huawei | EMUI 9 (Android 9) | âœ… Full |
| **Samsung Galaxy J7 Prime 2** | Samsung | One UI (Android 9) | âœ… Full |

**Compatibility target:** Android 7 â†’ Android 16 on all major OEMs (Pixel, Samsung, Xiaomi, OnePlus, Oppo, Vivo, Huawei, Asus, Sony, Nothing, Motorola, Nokia, TCL, Tecno, Infinix).

---

## Build from Source

### Prerequisites

- **.NET 8 SDK** or newer
- **Inno Setup 6 or 7** (only for building the installer)
- **Git**

### Steps

```powershell
# 1. Clone
git clone https://github.com/DeZ4p/DeZ4pAndroidManager.git
cd DeZ4pAndroidManager

# 2. Restore
dotnet restore

# 3. Build (Debug)
dotnet build

# 4. Run
dotnet run --project src\DeZ4pAndroidManager\DeZ4pAndroidManager.csproj

# 5. Build Release (portable)
dotnet publish src\DeZ4pAndroidManager\DeZ4pAndroidManager.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 6. Build Installer (requires Inno Setup)
cd installer
.\build-installer.ps1
```

---

## Project Structure

```
DeZ4pAndroidManager/
â”œâ”€â”€ src/DeZ4pAndroidManager/
â”‚   â”œâ”€â”€ Controls/         Sparkline chart control
â”‚   â”œâ”€â”€ Converters/       XAML value converters
â”‚   â”œâ”€â”€ Localization/     Strings.en.xaml + Strings.fa.xaml
â”‚   â”œâ”€â”€ Models/           35+ data models
â”‚   â”œâ”€â”€ Services/         42 service classes (ADB, Fastboot, Analytics, ...)
â”‚   â”œâ”€â”€ Themes/           DarkTheme.xaml + LightTheme.xaml
â”‚   â”œâ”€â”€ ViewModels/       37 view models (MVVM)
â”‚   â”œâ”€â”€ Views/            38 XAML views
â”‚   â”œâ”€â”€ tools/
â”‚   â”‚   â”œâ”€â”€ platform-tools/    adb.exe + fastboot.exe
â”‚   â”‚   â””â”€â”€ scrcpy/            scrcpy v5.0 (screen mirror)
â”‚   â”œâ”€â”€ App.xaml
â”‚   â”œâ”€â”€ MainWindow.xaml
â”‚   â””â”€â”€ app.manifest
â”œâ”€â”€ installer/
â”‚   â”œâ”€â”€ DeZ4pAndroidManager.iss
â”‚   â”œâ”€â”€ installer-readme.txt
â”‚   â””â”€â”€ build-installer.ps1
â”œâ”€â”€ .github/workflows/
â”‚   â”œâ”€â”€ build.yml
â”‚   â”œâ”€â”€ release.yml
â”‚   â””â”€â”€ codeql.yml
â”œâ”€â”€ docs/screenshots/
â”œâ”€â”€ LICENSE
â””â”€â”€ README.md
```

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Language | C# 12 |
| Runtime | .NET 8 |
| UI | WPF (XAML) |
| Pattern | MVVM + Dependency Injection |
| DI | Microsoft.Extensions.DependencyInjection 8.0 |
| ADB | AdvancedSharpAdbClient 3.6.16 |
| Image processing | System.Drawing.Common 8.0 |
| Installer | Inno Setup 7 |
| CI/CD | GitHub Actions |

---

## Reporting Bugs

Found a bug? Please open an issue:

**[â†’ Open a Bug Report](https://github.com/DeZ4p/DeZ4pAndroidManager/issues/new?template=bug_report.yml)**

Please include:

- **Windows version** â€” open `winver`
- **App version** â€” from the sidebar
- **Device model + Android version**
- **Steps to reproduce**
- **Expected vs actual behavior**
- **Logs** â€” from `%LOCALAPPDATA%\DeZ4pAndroidManager\activity.json`
- **Screenshots** if UI-related

### Reporting Security Issues

**Do NOT open a public issue for security vulnerabilities.**
Report privately: **[Security Advisory](https://github.com/DeZ4p/DeZ4pAndroidManager/security/advisories/new)**

---

## License

This project is licensed under the **MIT License** â€” see [LICENSE](LICENSE) for details.

You are free to use, modify, and distribute this software, including for commercial purposes.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

### Quick Rules

- Language: C# 12 / .NET 8
- Pattern: strict MVVM
- Every file starts with `// (c) DeZ4p | t.me/DeZ4p | All Rights Reserved`
- No `TODO`s, no stubs, no placeholders
- UI text in English (use LocalizationService)
- `dotnet build -c Release` must succeed with **zero warnings**

---

## Support

- **Telegram:** [t.me/DeZ4p](https://t.me/DeZ4p)
- **Issues:** [GitHub Issues](https://github.com/DeZ4p/DeZ4pAndroidManager/issues)
- **Discussions:** [GitHub Discussions](https://github.com/DeZ4p/DeZ4pAndroidManager/discussions)

---
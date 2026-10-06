<!-- (c) DeZ4p | t.me/DeZ4p | All Rights Reserved -->

# Changelog

All notable changes to **DeZ4p Android Manager** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-10-06

### First stable release! 🎉

This is the first production-ready release of DeZ4p Android Manager.

### Added

#### Monitoring & Info
- Live Dashboard with real-time battery, RAM, CPU, storage, network metrics
- Composite Health Score (battery, temperature, storage, RAM, cycles)
- Top Apps list by CPU and RAM usage
- Battery Lab (cycle count, design capacity, current capacity, health, age)
- Thermal zone monitoring (CPU, GPU, battery, skin)
- Storage Analyzer with per-category breakdown
- Sensors Panel with live values
- Comprehensive Device Info page (60+ fields)

#### File & App Management
- Full File Manager (browse, download, upload, rename, delete, create folders)
- Multi-select with copy / cut / paste
- APK Installer (single `.apk` + split `.apks` via install-multiple)
- Apps Manager (list, info, uninstall)
- APK Backup (extract APKs from device)
- Split APK Tools
- Backup & Restore
- Media Gallery (photos, videos, audio)
- Contacts & SMS reader

#### Flash & Bootloader
- Flash Tools (boot, recovery, system, vendor)
- Bootloader Tools (unlock, lock, getvar, OEM commands)
- Recovery Manager (reboot, sideload, ADB in recovery)
- Partition Tools (list, backup individual partitions)
- Reboot Options (Normal, Recovery, Bootloader, Fastbootd, EDL, Shutdown, SystemUI)

#### Advanced
- ADB Console with output capture
- Fastboot Console
- Logcat Viewer with filter
- Process Manager (CPU, RAM, PID)
- Network Tools (WiFi, mobile, signal)
- Script Runner
- Task Scheduler

#### Security & Privacy
- Security Center (SELinux, root, verified boot)
- Permissions Manager
- Privacy Tools
- Wireless ADB (Wi-Fi pairing)

#### Media
- Screen Mirror (bundled scrcpy v5.0)
- Screenshot tool
- Screen recording to MP4

#### UI / UX
- Bilingual interface (English + Persian) with full RTL support
- Dark & Light themes (auto-detect from Windows + manual toggle)
- Smooth animations (60 FPS)
- Full DPI awareness (100%, 125%, 150%, 200%)
- Device state gating (features auto-lock in incompatible modes)
- Hot-plug device detection
- Persistent Activity Log (500 events)
- Splash screen with fade transitions
- Toast notifications

#### Distribution
- Self-contained build (no .NET runtime required)
- Single-file executable
- Silent admin elevation optional
- Inno Setup installer with Start Menu + optional Desktop shortcut
- Portable ZIP distribution
- File association for `.apks`

### Technical
- .NET 8 + WPF + strict MVVM
- Dependency Injection (Microsoft.Extensions.DependencyInjection 8.0)
- AdvancedSharpAdbClient 3.6.16
- System.Drawing.Common 8.0
- Inno Setup 7
- GitHub Actions CI/CD

### Compatibility
- **Windows:** 10 (build 17763 / 1809) → 11 latest · Server 2019/2022
- **Architecture:** x64 (AMD64)
- **Android:** 7 (API 24) → 16 (API 36)
- **ADB:** 1.0.36 → 1.0.41+

### Tested On
- Poco X6 Pro (HyperOS / Android 14)
- Poco C71 (MIUI / Android 13)
- Huawei P Smart 2019 (EMUI 9 / Android 9)
- Samsung Galaxy J7 Prime 2 (One UI / Android 9)

### Known Issues
- 16×16 and 24×24 icon sizes may appear soft on complex logos (Windows icon limitation, not a bug)

---

## [Unreleased]

### Planned
- Wi-Fi file transfer without ADB
- Device backup to cloud
- Custom icon themes
- Batch APK installation
- Android 17 (API 37) support once released
- Plugin system for custom tools

---

[1.0.0]: https://github.com/DeZ4p/DeZ4pAndroidManager/releases/tag/v1.0.0
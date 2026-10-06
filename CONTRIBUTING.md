<!-- (c) DeZ4p | t.me/DeZ4p | All Rights Reserved -->

# Contributing to DeZ4p Android Manager

First off — **thank you** for considering contributing! 🎉

Every contribution helps make this tool better for the entire Android community.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Commit Convention](#commit-convention)
- [Pull Request Process](#pull-request-process)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)
- [Testing](#testing)
- [License](#license)

---

## Code of Conduct

By participating, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).
Please report unacceptable behavior to [@DeZ4p](https://t.me/DeZ4p).

---

## How Can I Contribute?

There are many ways to contribute:

### 🐛 Report Bugs
Open an issue using the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.yml).

### 💡 Suggest Features
Open an issue using the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.yml).

### 📝 Improve Documentation
Fix typos, clarify confusing sections, add examples — all welcome.

### 🌍 Add Translations
Currently supported: **English + Persian**. Want to add a new language? Open a discussion first.

### 🎨 Improve UI / UX
Better icons, animations, layouts — send a PR with screenshots.

### 🔧 Fix Bugs
Check [open issues labeled "good first issue"](https://github.com/DeZ4p/DeZ4pAndroidManager/labels/good%20first%20issue).

### ✨ Add Features
Check the [Roadmap in the README](README.md#planned) or open a discussion first.

---

## Development Setup

### Prerequisites

- **Windows 10 (1809+)** or **Windows 11**
- **.NET 8 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (optional) or **JetBrains Rider** or **VS Code** with C# extension
- **Inno Setup 6 or 7** — only if building the installer ([download](https://jrsoftware.org/isdl.php))
- **Git**

### First-time Setup

```powershell
# 1. Fork the repository on GitHub, then clone your fork
git clone https://github.com/YOUR-USERNAME/DeZ4pAndroidManager.git
cd DeZ4pAndroidManager

# 2. Add the upstream remote
git remote add upstream https://github.com/DeZ4p/DeZ4pAndroidManager.git

# 3. Restore NuGet packages
dotnet restore

# 4. Build in Debug
dotnet build

# 5. Run the application
dotnet run --project src\DeZ4pAndroidManager\DeZ4pAndroidManager.csproj

# 6. (Optional) Build the installer
cd installer
.\build-installer.ps1
```

### Project Structure

See [README.md → Project Structure](README.md#project-structure).

---

## Coding Standards

### Language & Framework

- **Language:** C# 12
- **Framework:** .NET 8 (`net8.0-windows`)
- **UI:** WPF (XAML)
- **Pattern:** Strict MVVM — no business logic in code-behind

### File Header (mandatory)

Every new source file must start with:

```csharp
// (c) DeZ4p | t.me/DeZ4p | All Rights Reserved
```

For XAML files:

```xml
<!-- (c) DeZ4p | t.me/DeZ4p | All Rights Reserved -->
```

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Public members | PascalCase | `DeviceInfoService` |
| Private fields | `_camelCase` | `_adbPath` |
| Local variables | camelCase | `deviceCount` |
| Constants | PascalCase | `DefaultTimeoutMs` |
| Interfaces | `I` + PascalCase | `IDeviceService` |

### Code Style

- **Implicit usings** — enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Nullable reference types** — enabled (`<Nullable>enable</Nullable>`)
- **File-scoped namespaces** — preferred: `namespace Foo;`
- **Braces** — Allman style (opening brace on new line)
- **Braces required** — always, even for single-line `if`
- **Line length** — soft limit 120 characters
- **var** — use when type is obvious from the right side

### Async

- **All I/O is async** — no `.Result`, no `.Wait()`, no `.GetAwaiter().GetResult()`
- **Cancellation tokens** — every long-running method should accept `CancellationToken ct`
- **ConfigureAwait** — not needed in WPF app code (UI thread matters)

### Error Handling

- **try/catch every I/O operation**
- **Never swallow exceptions silently** — at minimum, log them
- **User-facing errors** — show meaningful messages, not stack traces
- **Global handler** — `DispatcherUnhandledException` is already wired in `App.xaml.cs`

### Comments & Documentation

- **XML docs (`///`)** — on all public APIs
- **Inline comments** — English only
- **No commented-out code** — delete it, git has history

### UI Text

- **All UI text in English** — no hardcoded Persian strings
- **Use `LocalizationService.Translate("Key")`** for any user-visible string
- **Add both `Strings.en.xaml` and `Strings.fa.xaml`** entries for any new key

### Forbidden

❌ `TODO` comments — either finish the work or open an issue
❌ Placeholder / stub methods
❌ Empty catch blocks: `catch { }`
❌ Hardcoded strings in XAML
❌ Hardcoded file paths like `C:\Users\...`
❌ Hardcoded secrets / API keys
❌ Deprecated NuGet packages
❌ `Thread.Sleep` in UI code — use `await Task.Delay`

---

## Commit Convention

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body (optional)>

<footer (optional)>
```

### Types

| Type | When to use |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, no code change |
| `refactor` | Code restructure, no behavior change |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build tools, dependencies, CI |
| `ci` | CI/CD changes |
| `security` | Security-related changes |

### Examples

```
feat(file-manager): add multi-select copy
fix(adb): handle timeout on slow devices
docs(readme): update install instructions
refactor(services): extract DeviceInfoService parser
perf(dashboard): cache sparkline points
chore(deps): bump AdvancedSharpAdbClient to 3.6.17
```

### Rules

- **Subject line** — imperative, lowercase, no period, max 72 chars
- **Body** — wrap at 72 chars, explain *what* and *why*, not *how*
- **One logical change per commit**

---

## Pull Request Process

### 1. Before You Start

- **Open an issue** for non-trivial changes — discuss before coding
- **One feature per PR** — do not mix unrelated changes
- **Fork** the repository

### 2. Create a Branch

```powershell
git checkout -b feature/your-feature-name
# or
git checkout -b fix/your-bug-name
```

Branch naming:

- `feature/...` — new features
- `fix/...` — bug fixes
- `docs/...` — documentation
- `refactor/...` — refactoring
- `chore/...` — maintenance

### 3. Make Your Changes

- Follow the [Coding Standards](#coding-standards)
- Add XML docs for new public APIs
- Update `CHANGELOG.md` under `[Unreleased]`
- Update `README.md` if user-facing behavior changed

### 4. Test Locally

```powershell
dotnet build -c Release -warnaserror
```

Must succeed with **zero warnings** and **zero errors**.

### 5. Commit

Follow [Commit Convention](#commit-convention).

### 6. Push & Open PR

```powershell
git push origin feature/your-feature-name
```

Then open a PR on GitHub using the [PR template](.github/PULL_REQUEST_TEMPLATE.md).

### 7. PR Checklist

Your PR description will contain this checklist — fill it in honestly:

- [ ] My code follows the coding standards
- [ ] I have performed a self-review
- [ ] `dotnet build -c Release` succeeds with zero warnings
- [ ] No `TODO`s or stubs
- [ ] All new files have the DeZ4p header
- [ ] UI text is localized (not hardcoded)
- [ ] Updated `CHANGELOG.md`
- [ ] Tested on Windows 10 or 11
- [ ] (If UI) Tested at 100%, 125%, 150% DPI

### 8. Review Process

- A maintainer will review within **7 days**
- Address feedback by pushing more commits to your branch
- Once approved, a maintainer will squash-merge

---

## Reporting Bugs

Use the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.yml).

Include:

- Windows version (`winver`)
- App version (from sidebar)
- .NET version (`dotnet --version`)
- ADB version (`adb version`)
- Device model + Android version
- Steps to reproduce
- Expected vs actual behavior
- Logs from `%LOCALAPPDATA%\DeZ4pAndroidManager\activity.json`
- Screenshots if UI-related

---

## Suggesting Features

Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.yml).

Describe:

- The problem it solves
- Your proposed solution
- Alternatives considered
- Category (Dashboard, File Manager, Flash, etc.)
- Priority from your point of view

---

## Testing

### Manual Testing Checklist

Before submitting a PR, test:

- [ ] App starts without errors
- [ ] Device is detected when plugged in
- [ ] Device is removed cleanly when unplugged
- [ ] All navigation menu items work
- [ ] Theme toggle (dark ↔ light) works
- [ ] Language toggle (EN ↔ FA) works, RTL is correct
- [ ] Lock overlay appears when no device
- [ ] Access gate appears in wrong device mode
- [ ] Screenshot capture works
- [ ] No memory leaks (task manager check)

### Device Testing

If you can, test on real hardware:

- **At least one Android 13+** device
- **At least one Android 9 or older** device
- **Different OEMs** if possible (Samsung, Xiaomi, etc.)

---

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).

---

## Questions?

- **Telegram:** [t.me/DeZ4p](https://t.me/DeZ4p)
- **GitHub Discussions:** [DeZ4pAndroidManager/discussions](https://github.com/DeZ4p/DeZ4pAndroidManager/discussions)

---

<div align="center">

**Thank you for contributing! ❤️**

**© DeZ4p | [t.me/DeZ4p](https://t.me/DeZ4p) | All Rights Reserved**

</div>
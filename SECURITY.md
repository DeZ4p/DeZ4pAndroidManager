<!-- (c) DeZ4p | t.me/DeZ4p | All Rights Reserved -->

# Security Policy

## Supported Versions

We actively support the following versions with security updates:

| Version | Supported |
| ------- | --------- |
| 1.0.x   | ✅ Active support |
| < 1.0   | ❌ Not supported |

---

## Reporting a Vulnerability

**Please do NOT open a public GitHub issue for security vulnerabilities.**

Public disclosure of security issues puts all users at risk.

### How to Report Privately

Choose one of the following:

**1. GitHub Security Advisory (recommended)**

[→ Open a private security advisory](https://github.com/DeZ4p/DeZ4pAndroidManager/security/advisories/new)

**2. Telegram (for urgent cases)**

Contact: [@DeZ4p](https://t.me/DeZ4p)

---

## What to Include

Please provide the following information to help us triage quickly:

- **Vulnerability type** — e.g. command injection, privilege escalation, information disclosure, DoS
- **Affected component** — which service, view, or feature
- **Affected version(s)** — version number(s) where the issue exists
- **Steps to reproduce** — clear, minimal steps
- **Proof of concept** — code or script if applicable
- **Impact assessment** — what an attacker could achieve
- **Suggested fix** — optional but helpful
- **Your name/handle** — for credit (optional, we respect anonymity)

---

## Our Commitment

When you report a vulnerability, we will:

| Timeframe | Action |
|-----------|--------|
| **Within 72 hours** | Acknowledge receipt of your report |
| **Within 7 days** | Provide initial assessment and severity |
| **Within 30 days** | Provide a fix or mitigation plan |
| **After fix release** | Publicly credit you (unless you prefer anonymity) |

---

## Security Design of This Application

This application is designed with security in mind:

### General Principles

- **No hardcoded secrets** — no API keys, tokens, or passwords in source
- **No telemetry** — no data leaves your machine
- **No cloud dependency** — fully offline operation
- **No admin requirement** — runs as standard user by default
- **Minimal privileges** — installer admin elevation is optional

### ADB / Fastboot Command Safety

- **Argument-array invocation** — no shell string interpolation (prevents injection)
- **Timeouts enforced** — every external command has a hard timeout
- **Cancellation tokens** — all long-running operations are cancellable
- **Path validation** — user-supplied paths are validated before passing to ADB
- **No `cmd.exe` wrapper** — commands are executed directly

### Data Handling

- **Activity log** — stored locally in `%LOCALAPPDATA%\DeZ4pAndroidManager\`
- **No automatic upload** — nothing is ever sent anywhere
- **No device data leaves your PC** — screenshots, files, contacts stay local

### Update Safety

- **Signed release builds** — all official releases are built on GitHub Actions
- **Source is public** — full transparency, reproducible builds
- **Dependency scanning** — Dependabot + CodeQL run on every push

---

## Security Best Practices for Users

### For Your Own Safety

- **Only download from official sources:**
  - GitHub Releases: https://github.com/DeZ4p/DeZ4pAndroidManager/releases
  - Never trust copies from file-sharing sites or third-party mirrors

- **Verify the file hash** — releases include SHA256 checksums

- **Keep your tools up to date** — use the latest release

- **Enable Windows Defender** — it should scan downloads automatically

### When Using USB Debugging

- **Only enable USB Debugging** on devices you trust and control
- **Always verify the computer fingerprint** before approving the debugging prompt
- **Revoke USB debugging authorizations** when done with a shared computer
- **Disable USB Debugging** if you are not actively using it

### When Using Wireless ADB

- **Only pair over trusted networks** — never on public Wi-Fi
- **Use Android 11+ pairing codes** — more secure than legacy ADB over TCP/IP
- **Unpair when done** — do not leave wireless ADB enabled 24/7

---

## Dependencies

This project relies on the following trusted dependencies, all audited by Dependabot:

| Package | Purpose | License |
|---------|---------|---------|
| AdvancedSharpAdbClient | ADB protocol | MIT |
| Microsoft.Extensions.DependencyInjection | Dependency injection | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | Local storage | MIT |
| System.Drawing.Common | Image processing | MIT |

Bundled tools:

| Tool | Source | License |
|------|--------|---------|
| Android Platform Tools (adb, fastboot) | Google | Apache 2.0 |
| scrcpy | Genymobile | Apache 2.0 |

---

## Disclosure Policy

We follow **coordinated disclosure**:

1. You report privately
2. We acknowledge within 72 hours
3. We develop and test a fix
4. We prepare a release with the fix
5. We publicly disclose after the fix is available
6. You receive credit (unless you prefer otherwise)

We will not pursue legal action against researchers who report vulnerabilities in good faith and follow this policy.

---

## Hall of Fame

Security researchers who have responsibly disclosed issues will be listed here (with permission):

*No reports yet — this project is new as of 1.0.0.*

---

## Contact

- **Private security reports:** [GitHub Security Advisories](https://github.com/DeZ4p/DeZ4pAndroidManager/security/advisories/new)
- **Telegram:** [t.me/DeZ4p](https://t.me/DeZ4p)
- **General issues (non-security):** [GitHub Issues](https://github.com/DeZ4p/DeZ4pAndroidManager/issues)

---

**© DeZ4p | [t.me/DeZ4p](https://t.me/DeZ4p) | All Rights Reserved**
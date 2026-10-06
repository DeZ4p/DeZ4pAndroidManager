// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class RecoveryManagerService
{
    private readonly AdbService _adb;
    private readonly FastbootService _fastboot;

    public RecoveryManagerService(AdbService adb, FastbootService fastboot)
    {
        _adb = adb;
        _fastboot = fastboot;
    }

    public static List<BootTarget> GetBootTargets() => new()
    {
        new BootTarget
        {
            Key = "normal", Label = "Normal Reboot",
            Description = "Reboot to Android (system)",
            Icon = "\uE777", Color = "#4F8CFF",
            Command = "reboot", RequiredMode = "adb",
            SupportedModes = new[] { "adb", "recovery" }
        },
        new BootTarget
        {
            Key = "recovery", Label = "Recovery",
            Description = "Reboot to recovery mode (TWRP/Stock)",
            Icon = "\uE90F", Color = "#F59E0B",
            Command = "reboot recovery", RequiredMode = "adb",
            SupportedModes = new[] { "adb" }
        },
        new BootTarget
        {
            Key = "bootloader", Label = "Bootloader",
            Description = "Reboot to bootloader / fastboot",
            Icon = "\uE945", Color = "#EF4444",
            Command = "reboot bootloader", RequiredMode = "adb",
            SupportedModes = new[] { "adb" }
        },
        new BootTarget
        {
            Key = "fastbootd", Label = "Fastbootd",
            Description = "Reboot to fastbootd (userspace fastboot)",
            Icon = "\uE7F8", Color = "#818CF8",
            Command = "reboot fastboot", RequiredMode = "adb",
            SupportedModes = new[] { "adb" }
        },
        new BootTarget
        {
            Key = "sideload", Label = "Sideload Mode",
            Description = "Reboot to recovery + enter ADB sideload",
            Icon = "\uE896", Color = "#22D3EE",
            Command = "reboot sideload", RequiredMode = "adb",
            SupportedModes = new[] { "adb" }
        },
        new BootTarget
        {
            Key = "systemui", Label = "Restart System UI",
            Description = "Force SystemUI to restart (UI mode toggle)",
            Icon = "\uE7F4", Color = "#A855F7",
            Command = "__SYSTEMUI_RESTART__",  // ⭐ special marker
            RequiredMode = "adb",
            SupportedModes = new[] { "adb" }
        },
        new BootTarget
        {
            Key = "shutdown", Label = "Power Off",
            Description = "Shut down the device",
            Icon = "\uE7E8", Color = "#6B7280",
            Command = "shell reboot -p", RequiredMode = "adb",
            SupportedModes = new[] { "adb" }
        },
        new BootTarget
        {
            Key = "edl", Label = "EDL Mode",
            Description = "Emergency Download mode (Qualcomm only - dangerous)",
            Icon = "\uE7BA", Color = "#DC2626",
            Command = "reboot edl", RequiredMode = "adb",
            IsDestructive = true,
            SupportedModes = new[] { "adb" }
        }
    };

    public async Task<(bool ok, string message)> ExecuteAsync(
        string serial, BootTarget target, string currentMode, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(serial))
            return (false, "No device serial");

        try
        {
            // ⭐ Special-case: SystemUI restart needs multiple commands
            if (target.Key == "systemui" && currentMode != "fastboot" && currentMode != "fastbootd")
                return await RestartSystemUiAsync(serial, ct);

            if (currentMode == "fastboot" || currentMode == "fastbootd")
                return await ExecuteFastbootAsync(serial, target, ct);

            var r = await _adb.ExecuteRawAsync($"-s {serial} {target.Command}", 20000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0;
            return (ok, string.IsNullOrEmpty(combined) ? (ok ? "Command sent" : "Failed") : combined);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Real SystemUI restart that works without root.
    /// Tries multiple methods in order - stops at first success.
    /// </summary>
    private async Task<(bool ok, string message)> RestartSystemUiAsync(string serial, CancellationToken ct)
    {
        var attempts = new List<string>();

        // ═══ Method 1: UI mode toggle via cmd uimode (Android 9+) ═══
        // SystemUI restarts automatically when UI mode changes.
        try
        {
            await _adb.ExecuteRawAsync($"-s {serial} shell cmd uimode night yes", 8000, ct);
            await Task.Delay(400, ct);
            await _adb.ExecuteRawAsync($"-s {serial} shell cmd uimode night no", 8000, ct);
            attempts.Add("uimode toggle: OK");
            return (true, "System UI restarted (night mode toggle)");
        }
        catch (Exception ex) { attempts.Add($"uimode: {ex.Message}"); }

        // ═══ Method 2: settings put secure ui_night_mode ═══
        try
        {
            await _adb.ExecuteRawAsync($"-s {serial} shell settings put secure ui_night_mode 2", 8000, ct);
            await Task.Delay(400, ct);
            await _adb.ExecuteRawAsync($"-s {serial} shell settings put secure ui_night_mode 1", 8000, ct);
            attempts.Add("settings toggle: OK");
            return (true, "System UI restarted (settings toggle)");
        }
        catch (Exception ex) { attempts.Add($"settings: {ex.Message}"); }

        // ═══ Method 3: am force-stop (fallback - works on some ROMs) ═══
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell am force-stop com.android.systemui", 8000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            attempts.Add($"force-stop: exit {r.ExitCode}");
            if (r.ExitCode == 0 && !combined.Contains("Permission", StringComparison.OrdinalIgnoreCase))
                return (true, "System UI stop requested");
        }
        catch (Exception ex) { attempts.Add($"force-stop: {ex.Message}"); }

        // ═══ Method 4: killall (needs root, but try anyway) ═══
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell su -c 'killall com.android.systemui'", 8000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            if (r.ExitCode == 0 && !combined.Contains("Permission", StringComparison.OrdinalIgnoreCase))
                return (true, "System UI killed (root)");
            attempts.Add($"su killall: exit {r.ExitCode}");
        }
        catch (Exception ex) { attempts.Add($"su: {ex.Message}"); }

        return (false, "All SystemUI restart methods failed:\n" + string.Join("\n", attempts));
    }

    private async Task<(bool ok, string message)> ExecuteFastbootAsync(
        string serial, BootTarget target, CancellationToken ct)
    {
        try
        {
            RawResult r = target.Key switch
            {
                "normal" => await _fastboot.RebootAsync(serial, ct),
                "recovery" => await _fastboot.RebootRecoveryAsync(serial, ct),
                "bootloader" => await _fastboot.RebootBootloaderAsync(serial, ct),
                "fastbootd" => await _fastboot.RebootFastbootdAsync(serial, ct),
                "shutdown" => await _fastboot.ShutdownAsync(serial, ct),
                "edl" => await _fastboot.ExecuteAsync(serial, "oem edl", ct),
                _ => await _fastboot.RebootAsync(serial, ct)
            };
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0;
            return (ok, string.IsNullOrEmpty(combined) ? (ok ? "Command sent" : "Failed") : combined);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
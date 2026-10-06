// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class PrivacyService
{
    private readonly AdbService _adb;
    public PrivacyService(AdbService adb) => _adb = adb;

    public async Task<List<PrivacyCheck>> GetChecksAsync(string serial, CancellationToken ct = default)
    {
        var checks = new List<PrivacyCheck>();

        var script = string.Join("; ",
            "echo LOCATION_MODE=$(settings get secure location_mode)",
            "echo CAMERA_TOGGLE=$(settings get global camera_toggle)",
            "echo MIC_TOGGLE=$(settings get global microphone_toggle)",
            "echo AIRPLANE=$(settings get global airplane_mode_on)",
            "echo AUTO_TIME=$(settings get global auto_time)",
            "echo AUTO_TZ=$(settings get global auto_time_zone)",
            "echo USAGE_ACCESS=$(settings get secure enabled_accessibility_services)",
            "echo INSTALL_UNKNOWN=$(settings get secure install_non_market_apps)",
            "echo ADB_WIFI=$(settings get global adb_wifi_enabled)"
        );
        var raw = await _adb.ShellAsync(serial, script, ct) ?? "";

        string G(string key)
        {
            var m = Regex.Match(raw, $@"{key}=(.+?)(?:\r?\n|$)");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        // Location
        var loc = G("LOCATION_MODE");
        checks.Add(new PrivacyCheck
        {
            Title = "Location Services",
            Value = loc == "0" ? "Disabled" : loc == "1" ? "GPS only" : loc == "3" ? "High accuracy" : loc.Length > 0 ? $"Mode {loc}" : "N/A",
            Hint = loc == "0" ? "Location is off — best privacy" : "Location is enabled",
            Icon = "\uE1D0",
            Level = loc == "0" ? "good" : "warn"
        });

        // Camera
        var cam = G("CAMERA_TOGGLE");
        checks.Add(new PrivacyCheck
        {
            Title = "Camera Access",
            Value = cam == "0" ? "Blocked" : cam == "1" ? "Allowed" : "N/A",
            Hint = cam == "0" ? "Camera is disabled system-wide" : "Camera is available to apps",
            Icon = "\uE722",
            Level = cam == "0" ? "good" : "info"
        });

        // Microphone
        var mic = G("MIC_TOGGLE");
        checks.Add(new PrivacyCheck
        {
            Title = "Microphone Access",
            Value = mic == "0" ? "Muted" : mic == "1" ? "Active" : "N/A",
            Hint = mic == "0" ? "Microphone is muted system-wide" : "Microphone is available",
            Icon = "\uE720",
            Level = mic == "0" ? "good" : "info"
        });

        // Unknown sources
        var unk = G("INSTALL_UNKNOWN");
        checks.Add(new PrivacyCheck
        {
            Title = "Unknown App Installation",
            Value = unk == "1" ? "Allowed" : unk == "0" ? "Blocked" : "N/A",
            Hint = unk == "1" ? "Apps can be installed from unknown sources" : "Only official stores allowed",
            Icon = "\uE7BA",
            Level = unk == "1" ? "warn" : "good"
        });

        // Accessibility services
        var acc = G("USAGE_ACCESS");
        checks.Add(new PrivacyCheck
        {
            Title = "Accessibility Services",
            Value = string.IsNullOrEmpty(acc) || acc == "null" ? "None" : "Active",
            Hint = string.IsNullOrEmpty(acc) || acc == "null" ? "No services with accessibility access" : "Apps with accessibility access enabled",
            Icon = "\uE7F4",
            Level = string.IsNullOrEmpty(acc) || acc == "null" ? "good" : "warn"
        });

        // Auto time
        var at = G("AUTO_TIME");
        checks.Add(new PrivacyCheck
        {
            Title = "Auto Time Sync",
            Value = at == "1" ? "Enabled" : "Disabled",
            Hint = at == "1" ? "Time synced with network" : "Manual time",
            Icon = "\uE823",
            Level = "info"
        });

        // Auto timezone
        var atz = G("AUTO_TZ");
        checks.Add(new PrivacyCheck
        {
            Title = "Auto Timezone",
            Value = atz == "1" ? "Enabled" : "Disabled",
            Hint = atz == "1" ? "Timezone synced with network" : "Manual timezone",
            Icon = "\uE707",
            Level = "info"
        });

        return checks;
    }
}
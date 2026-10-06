// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class SecurityService
{
    private readonly AdbService _adb;
    public SecurityService(AdbService adb) => _adb = adb;

    public async Task<SecurityReport> GetAsync(string serial, CancellationToken ct = default)
    {
        var rpt = new SecurityReport();
        var script = string.Join("; ",
            "echo BOOTLOADER=$(getprop ro.boot.vbmeta.device_state)",
            "echo VERIFIED=$(getprop ro.boot.verifiedbootstate)",
            "echo SECUREBOOT=$(getprop ro.secureboot.lockstate)",
            "echo FLASHLOCKED=$(getprop ro.boot.flash.locked)",
            "echo SELINUX=$(getenforce 2>/dev/null)",
            "echo ENCRYPT=$(getprop ro.crypto.state)",
            "echo ROOTCHECK=$(which su 2>/dev/null | head -1)",
            "echo DEVOPT=$(settings get global development_settings_enabled)",
            "echo ADB_ENABLED=$(settings get global adb_enabled)",
            "echo WIFI_ADB=$(settings get global adb_wifi_enabled)",
            "echo UNKNOWN_SRC=$(settings get secure install_non_market_apps)",
            "echo SCREEN_LOCK=$(dumpsys trust 2>/dev/null | grep -m1 -o 'DeviceLocked: [a-z]*')",
            "echo ADB_PORT=$(getprop service.adb.tcp.port)"
        );
        var raw = await _adb.ShellAsync(serial, script, ct) ?? "";

        string G(string key)
        {
            var m = Regex.Match(raw, $@"{key}=(.+?)(?:\r?\n|$)");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        // Bootloader
        var bl = G("BOOTLOADER");
        if (bl == "locked") rpt.Checks.Add(C("Bootloader", "Locked", "Bootloader is secure", "\uE72E", "good"));
        else if (bl == "unlocked") rpt.Checks.Add(C("Bootloader", "Unlocked", "Bootloader is unlocked — security risk", "\uE785", "bad"));
        else rpt.Checks.Add(C("Bootloader", "N/A", "Not detectable", "\uE72E", "info"));

        // Verified boot
        var vb = G("VERIFIED");
        if (vb == "green") rpt.Checks.Add(C("Verified Boot", "Green (Verified)", "System verified by OEM key", "\uE73E", "good"));
        else if (vb == "orange") rpt.Checks.Add(C("Verified Boot", "Orange (Unlocked)", "Custom key or unlocked", "\uE7BA", "warn"));
        else if (vb == "red") rpt.Checks.Add(C("Verified Boot", "Red (Failed)", "Verification failed", "\uE783", "bad"));
        else rpt.Checks.Add(C("Verified Boot", "N/A", "Not detectable", "\uE73E", "info"));

        // SELinux
        var se = G("SELINUX");
        if (se == "Enforcing") rpt.Checks.Add(C("SELinux", "Enforcing", "SELinux is enforcing", "\uE72E", "good"));
        else if (se == "Permissive") rpt.Checks.Add(C("SELinux", "Permissive", "SELinux is permissive", "\uE7BA", "warn"));
        else rpt.Checks.Add(C("SELinux", se.Length > 0 ? se : "N/A", "Not detectable", "\uE72E", "info"));

        // Encryption
        var enc = G("ENCRYPT");
        if (enc == "encrypted") rpt.Checks.Add(C("Storage Encryption", "Encrypted", "User data is encrypted", "\uE72E", "good"));
        else if (enc == "unencrypted") rpt.Checks.Add(C("Storage Encryption", "Not Encrypted", "Data is not encrypted", "\uE785", "bad"));
        else rpt.Checks.Add(C("Storage Encryption", "N/A", "Not detectable", "\uE72E", "info"));

        // Root
        var root = G("ROOTCHECK");
        if (string.IsNullOrEmpty(root) || root.Contains("not found"))
            rpt.Checks.Add(C("Root Access", "Not rooted", "Device is not rooted", "\uE72E", "good"));
        else
            rpt.Checks.Add(C("Root Access", "Rooted", "Root binary detected", "\uE7BA", "warn"));

        // Developer options
        var devOpt = G("DEVOPT");
        if (devOpt == "0") rpt.Checks.Add(C("Developer Options", "Disabled", "Hidden from settings", "\uE72E", "good"));
        else if (devOpt == "1") rpt.Checks.Add(C("Developer Options", "Enabled", "Developer options are on", "\uE7BA", "warn"));
        else rpt.Checks.Add(C("Developer Options", "N/A", "Not detectable", "\uE72E", "info"));

        // ADB
        var adb = G("ADB_ENABLED");
        if (adb == "1") rpt.Checks.Add(C("USB Debugging", "Enabled", "USB debugging is on", "\uE7BA", "warn"));
        else if (adb == "0") rpt.Checks.Add(C("USB Debugging", "Disabled", "USB debugging is off", "\uE72E", "good"));
        else rpt.Checks.Add(C("USB Debugging", "N/A", "Not detectable", "\uE72E", "info"));

        // WiFi ADB
        var wifiAdb = G("WIFI_ADB");
        var adbPort = G("ADB_PORT");
        bool wifiOn = wifiAdb == "1" || (!string.IsNullOrEmpty(adbPort) && adbPort != "-1" && adbPort != "0");
        if (wifiOn) rpt.Checks.Add(C("Wireless ADB", "Enabled", "ADB over network is on", "\uE701", "warn"));
        else rpt.Checks.Add(C("Wireless ADB", "Disabled", "ADB over network is off", "\uE72E", "good"));

        // Unknown sources
        var unknown = G("UNKNOWN_SRC");
        if (unknown == "1") rpt.Checks.Add(C("Unknown Sources", "Allowed", "Install from unknown apps", "\uE7BA", "warn"));
        else if (unknown == "0") rpt.Checks.Add(C("Unknown Sources", "Blocked", "Only from official stores", "\uE72E", "good"));
        else rpt.Checks.Add(C("Unknown Sources", "N/A", "Not detectable", "\uE72E", "info"));

        // Screen lock
        var lock_ = G("SCREEN_LOCK");
        if (!string.IsNullOrEmpty(lock_))
        {
            var isLocked = lock_.Contains("true", StringComparison.OrdinalIgnoreCase);
            rpt.Checks.Add(C("Screen Lock", isLocked ? "Active" : "Inactive",
                isLocked ? "Lock screen is protecting device" : "No lock screen",
                "\uE72E", isLocked ? "good" : "warn"));
        }
        else
        {
            rpt.Checks.Add(C("Screen Lock", "N/A", "Not detectable", "\uE72E", "info"));
        }

        // Calculate score
        int total = 0, possible = 0;
        foreach (var chk in rpt.Checks)
        {
            if (chk.Level == "info") continue;
            possible += 10;
            if (chk.Level == "good") total += 10;
            else if (chk.Level == "warn") total += 5;
        }
        rpt.Score = possible > 0 ? (int)Math.Round((double)total / possible * 100) : 0;
        rpt.Grade = rpt.Score switch
        {
            >= 90 => "Excellent",
            >= 75 => "Good",
            >= 60 => "Fair",
            >= 40 => "Poor",
            _     => "Critical"
        };

        return rpt;
    }

    private static SecurityCheck C(string t, string v, string h, string icon, string lvl)
        => new SecurityCheck { Title = t, Value = v, Hint = h, Icon = icon, Level = lvl };
}
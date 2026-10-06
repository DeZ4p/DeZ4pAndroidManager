// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class ConsoleResult
{
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = "";
    public string StdErr { get; set; } = "";
    public long DurationMs { get; set; }
}

public class ConsoleService
{
    private readonly AdbService _adb;
    private readonly FastbootService _fastboot;

    public ConsoleService(AdbService adb, FastbootService fastboot)
    {
        _adb = adb;
        _fastboot = fastboot;
    }

    // ═══════════════════════════════════════════════════════════
    //  RUNNERS
    // ═══════════════════════════════════════════════════════════
    public async Task<ConsoleResult> RunAdbShellAsync(string serial, string shellCmd, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell {shellCmd}", 30000, ct);
            sw.Stop();
            return new ConsoleResult
            {
                ExitCode = r.ExitCode,
                StdOut = r.StandardOutput ?? "",
                StdErr = r.StandardError ?? "",
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConsoleResult { ExitCode = -1, StdErr = ex.Message, DurationMs = sw.ElapsedMilliseconds };
        }
    }

    public async Task<ConsoleResult> RunAdbRawAsync(string serial, string rawArgs, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var prefix = string.IsNullOrEmpty(serial) ? "" : $"-s {serial} ";
            var r = await _adb.ExecuteRawAsync(prefix + rawArgs, 60000, ct);
            sw.Stop();
            return new ConsoleResult
            {
                ExitCode = r.ExitCode,
                StdOut = r.StandardOutput ?? "",
                StdErr = r.StandardError ?? "",
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConsoleResult { ExitCode = -1, StdErr = ex.Message, DurationMs = sw.ElapsedMilliseconds };
        }
    }

    public async Task<ConsoleResult> RunFastbootAsync(string serial, string fbCmd, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Strip leading "fastboot " if present
            var cmd = fbCmd?.Trim() ?? "";
            if (cmd.StartsWith("fastboot ", StringComparison.OrdinalIgnoreCase))
                cmd = cmd.Substring(9).Trim();

            var r = await _fastboot.ExecuteAsync(serial, cmd, ct);
            sw.Stop();
            return new ConsoleResult
            {
                ExitCode = r.ExitCode,
                StdOut = r.StandardOutput ?? "",
                StdErr = r.StandardError ?? "",
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConsoleResult { ExitCode = -1, StdErr = ex.Message, DurationMs = sw.ElapsedMilliseconds };
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ADB PRESETS - 100+ commands, all start with "adb "
    // ═══════════════════════════════════════════════════════════
    public static List<PresetCommand> GetAdbPresets() => new()
    {
        // ═══ GLOBAL ═══
        new() { Category = "Global", Label = "List devices",       Command = "adb devices",          Description = "Show all connected ADB devices" },
        new() { Category = "Global", Label = "List devices (-l)",  Command = "adb devices -l",       Description = "Verbose list with model info" },
        new() { Category = "Global", Label = "ADB version",        Command = "adb version",          Description = "Show ADB client/server version" },
        new() { Category = "Global", Label = "Start server",       Command = "adb start-server",     Description = "Start the ADB server" },
        new() { Category = "Global", Label = "Kill server",        Command = "adb kill-server",      Description = "Stop the ADB server" },
        new() { Category = "Global", Label = "Show help",          Command = "adb help",             Description = "Show full adb help" },
        new() { Category = "Global", Label = "Track devices",      Command = "adb track-devices",    Description = "Monitor device changes live" },

        // ═══ DEVICE INFO (SHELL) ═══
        new() { Category = "Device", Label = "Model",              Command = "adb shell getprop ro.product.model" },
        new() { Category = "Device", Label = "Brand",              Command = "adb shell getprop ro.product.brand" },
        new() { Category = "Device", Label = "Manufacturer",       Command = "adb shell getprop ro.product.manufacturer" },
        new() { Category = "Device", Label = "Codename",           Command = "adb shell getprop ro.product.device" },
        new() { Category = "Device", Label = "Android version",    Command = "adb shell getprop ro.build.version.release" },
        new() { Category = "Device", Label = "SDK / API level",    Command = "adb shell getprop ro.build.version.sdk" },
        new() { Category = "Device", Label = "Build ID",           Command = "adb shell getprop ro.build.display.id" },
        new() { Category = "Device", Label = "Security patch",     Command = "adb shell getprop ro.build.version.security_patch" },
        new() { Category = "Device", Label = "Build date",         Command = "adb shell getprop ro.build.date" },
        new() { Category = "Device", Label = "Serial number",      Command = "adb shell getprop ro.serialno" },
        new() { Category = "Device", Label = "Bootloader state",   Command = "adb shell getprop ro.boot.vbmeta.device_state" },
        new() { Category = "Device", Label = "Verified boot",      Command = "adb shell getprop ro.boot.verifiedbootstate" },
        new() { Category = "Device", Label = "SELinux status",     Command = "adb shell getenforce" },
        new() { Category = "Device", Label = "Kernel",             Command = "adb shell uname -a" },
        new() { Category = "Device", Label = "Uptime",             Command = "adb shell cat /proc/uptime" },
        new() { Category = "Device", Label = "Hostname",           Command = "adb shell hostname" },
        new() { Category = "Device", Label = "Root check",         Command = "adb shell which su" },

        // ═══ SHELL - BASIC ═══
        new() { Category = "Shell", Label = "Whoami",              Command = "adb shell whoami" },
        new() { Category = "Shell", Label = "ID",                  Command = "adb shell id" },
        new() { Category = "Shell", Label = "Date",                Command = "adb shell date" },
        new() { Category = "Shell", Label = "Environment",         Command = "adb shell env" },
        new() { Category = "Shell", Label = "Busybox check",       Command = "adb shell which busybox" },

        // ═══ BATTERY ═══
        new() { Category = "Battery", Label = "Battery info",      Command = "adb shell dumpsys battery" },
        new() { Category = "Battery", Label = "Battery stats",     Command = "adb shell dumpsys batterystats" },
        new() { Category = "Battery", Label = "Raw uevent",        Command = "adb shell cat /sys/class/power_supply/battery/uevent" },
        new() { Category = "Battery", Label = "Cycle count",       Command = "adb shell cat /sys/class/power_supply/battery/cycle_count" },
        new() { Category = "Battery", Label = "Capacity (µAh)",    Command = "adb shell cat /sys/class/power_supply/battery/charge_full" },

        // ═══ STORAGE ═══
        new() { Category = "Storage", Label = "Disk free",         Command = "adb shell df -h" },
        new() { Category = "Storage", Label = "Disk usage /sdcard", Command = "adb shell du -sh /sdcard" },
        new() { Category = "Storage", Label = "List /sdcard",      Command = "adb shell ls -la /sdcard/" },
        new() { Category = "Storage", Label = "List /data",        Command = "adb shell ls -la /data/" },
        new() { Category = "Storage", Label = "List /system",      Command = "adb shell ls -la /system/" },
        new() { Category = "Storage", Label = "Mount points",      Command = "adb shell mount" },
        new() { Category = "Storage", Label = "Block devices",     Command = "adb shell ls -la /dev/block/" },

        // ═══ MEMORY ═══
        new() { Category = "Memory", Label = "Memory info",        Command = "adb shell cat /proc/meminfo" },
        new() { Category = "Memory", Label = "Meminfo (top 20)",   Command = "adb shell dumpsys meminfo | head -60" },
        new() { Category = "Memory", Label = "Swap",               Command = "adb shell cat /proc/swaps" },

        // ═══ CPU ═══
        new() { Category = "CPU", Label = "CPU info",              Command = "adb shell cat /proc/cpuinfo" },
        new() { Category = "CPU", Label = "Load average",          Command = "adb shell cat /proc/loadavg" },
        new() { Category = "CPU", Label = "CPU info (top)",        Command = "adb shell top -n 1 -b | head -20" },
        new() { Category = "CPU", Label = "Governor",              Command = "adb shell cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor" },
        new() { Category = "CPU", Label = "CPU frequencies",       Command = "adb shell cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq" },
        new() { Category = "CPU", Label = "CPU temperature",       Command = "adb shell cat /sys/class/thermal/thermal_zone0/temp" },

        // ═══ NETWORK ═══
        new() { Category = "Network", Label = "IP addresses",      Command = "adb shell ip addr show" },
        new() { Category = "Network", Label = "IP routing table",  Command = "adb shell ip route" },
        new() { Category = "Network", Label = "WiFi info",         Command = "adb shell dumpsys wifi | grep -m5 mWifiInfo" },
        new() { Category = "Network", Label = "WiFi country code", Command = "adb shell getprop ro.product.locale.region" },
        new() { Category = "Network", Label = "Network stats",     Command = "adb shell cat /proc/net/dev" },
        new() { Category = "Network", Label = "DNS servers",       Command = "adb shell getprop net.dns1" },
        new() { Category = "Network", Label = "Active connections",Command = "adb shell netstat -tun" },
        new() { Category = "Network", Label = "Ping Google DNS",   Command = "adb shell ping -c 4 8.8.8.8" },

        // ═══ PACKAGES ═══
        new() { Category = "Packages", Label = "All packages",     Command = "adb shell pm list packages" },
        new() { Category = "Packages", Label = "User packages (-3)", Command = "adb shell pm list packages -3" },
        new() { Category = "Packages", Label = "System packages (-s)", Command = "adb shell pm list packages -s" },
        new() { Category = "Packages", Label = "Disabled packages (-d)", Command = "adb shell pm list packages -d" },
        new() { Category = "Packages", Label = "Enabled packages (-e)", Command = "adb shell pm list packages -e" },
        new() { Category = "Packages", Label = "With paths (-f)",  Command = "adb shell pm list packages -f" },
        new() { Category = "Packages", Label = "All (short labels)", Command = "adb shell pm list packages -3 --user 0" },
        new() { Category = "Packages", Label = "WhatsApp paths",   Command = "adb shell pm path com.whatsapp" },
        new() { Category = "Packages", Label = "Instagram paths",  Command = "adb shell pm path com.instagram.android" },
        new() { Category = "Packages", Label = "Chrome paths",     Command = "adb shell pm path com.android.chrome" },

        // ═══ ACTIVITY MANAGER ═══
        new() { Category = "Activity", Label = "Start Settings",   Command = "adb shell am start -a android.settings.SETTINGS" },
        new() { Category = "Activity", Label = "Start Play Store", Command = "adb shell monkey -p com.android.vending 1" },
        new() { Category = "Activity", Label = "Force stop Chrome", Command = "adb shell am force-stop com.android.chrome" },
        new() { Category = "Activity", Label = "Force stop all",   Command = "adb shell am kill-all" },
        new() { Category = "Activity", Label = "Current activity", Command = "adb shell dumpsys activity activities | grep mResumedActivity" },
        new() { Category = "Activity", Label = "Recent tasks",     Command = "adb shell dumpsys activity recents | head -40" },
        new() { Category = "Activity", Label = "Running services", Command = "adb shell dumpsys activity services | head -60" },

        // ═══ DISPLAY ═══
        new() { Category = "Display", Label = "Screen size",       Command = "adb shell wm size" },
        new() { Category = "Display", Label = "Screen density",    Command = "adb shell wm density" },
        new() { Category = "Display", Label = "Reset size",        Command = "adb shell wm size reset" },
        new() { Category = "Display", Label = "Reset density",     Command = "adb shell wm density reset" },
        new() { Category = "Display", Label = "Display info",      Command = "adb shell dumpsys display | head -40" },

        // ═══ INPUT ═══
        new() { Category = "Input", Label = "Home key",            Command = "adb shell input keyevent 3" },
        new() { Category = "Input", Label = "Back key",            Command = "adb shell input keyevent 4" },
        new() { Category = "Input", Label = "Power key",           Command = "adb shell input keyevent 26" },
        new() { Category = "Input", Label = "Volume up",           Command = "adb shell input keyevent 24" },
        new() { Category = "Input", Label = "Volume down",         Command = "adb shell input keyevent 25" },
        new() { Category = "Input", Label = "Wake device",         Command = "adb shell input keyevent KEYCODE_WAKEUP" },
        new() { Category = "Input", Label = "Sleep device",        Command = "adb shell input keyevent KEYCODE_SLEEP" },

        // ═══ LOGS ═══
        new() { Category = "Logs", Label = "Logcat dump (last 50)", Command = "adb shell logcat -d -t 50" },
        new() { Category = "Logs", Label = "Clear logcat",         Command = "adb shell logcat -c" },
        new() { Category = "Logs", Label = "Logcat errors",        Command = "adb shell logcat -d -t 50 *:E" },
        new() { Category = "Logs", Label = "Dmesg (kernel)",       Command = "adb shell dmesg | tail -40" },

        // ═══ PROCESSES ═══
        new() { Category = "Process", Label = "Process list",      Command = "adb shell ps -A" },
        new() { Category = "Process", Label = "Processes (top)",   Command = "adb shell top -n 1 -b | head -25" },
        new() { Category = "Process", Label = "Process count",     Command = "adb shell ps -A | wc -l" },

        // ═══ SERVICES ═══
        new() { Category = "Services", Label = "Service list",     Command = "adb shell service list" },
        new() { Category = "Services", Label = "Dumpsys (short)",  Command = "adb shell dumpsys | head -60" },

        // ═══ FILES ═══
        new() { Category = "Files", Label = "Create test file",    Command = "adb shell touch /sdcard/dez4p_test.txt" },
        new() { Category = "Files", Label = "Delete test file",    Command = "adb shell rm -f /sdcard/dez4p_test.txt" },
        new() { Category = "Files", Label = "Find APKs",           Command = "adb shell find /sdcard -name '*.apk' 2>/dev/null" },
        new() { Category = "Files", Label = "Find images",         Command = "adb shell find /sdcard -name '*.jpg' 2>/dev/null | head -20" },
        new() { Category = "Files", Label = "Find videos",         Command = "adb shell find /sdcard -name '*.mp4' 2>/dev/null | head -20" },

        // ═══ ADVANCED ═══
        new() { Category = "Advanced", Label = "Reboot normal",    Command = "adb reboot",               Description = "Reboot to Android" },
        new() { Category = "Advanced", Label = "Reboot recovery",  Command = "adb reboot recovery" },
        new() { Category = "Advanced", Label = "Reboot bootloader",Command = "adb reboot bootloader" },
        new() { Category = "Advanced", Label = "Reboot fastboot",  Command = "adb reboot fastboot" },
        new() { Category = "Advanced", Label = "Power off",        Command = "adb shell reboot -p" },
        new() { Category = "Advanced", Label = "Root",             Command = "adb root" },
        new() { Category = "Advanced", Label = "Unroot",           Command = "adb unroot" },
        new() { Category = "Advanced", Label = "Remount",          Command = "adb remount" },
        new() { Category = "Advanced", Label = "Tcpip mode",       Command = "adb tcpip 5555" },
        new() { Category = "Advanced", Label = "Show features",    Command = "adb shell pm list features" },
        new() { Category = "Advanced", Label = "Show libraries",   Command = "adb shell pm list libraries" },
        new() { Category = "Advanced", Label = "System properties",Command = "adb shell getprop" },
        new() { Category = "Advanced", Label = "Re-connect",       Command = "adb reconnect" },
        new() { Category = "Advanced", Label = "USB disconnect",   Command = "adb usb" }
    };

    // ═══════════════════════════════════════════════════════════
    //  FASTBOOT PRESETS - all start with "fastboot "
    // ═══════════════════════════════════════════════════════════
    public static List<PresetCommand> GetFastbootPresets() => new()
    {
        // ═══ DEVICES ═══
        new() { Category = "Global", Label = "List devices",       Command = "fastboot devices",         Description = "Show all fastboot devices" },
        new() { Category = "Global", Label = "Help",               Command = "fastboot --help",          Description = "Show fastboot help" },

        // ═══ QUERY ═══
        new() { Category = "Query", Label = "Product name",        Command = "fastboot getvar product" },
        new() { Category = "Query", Label = "Bootloader version",  Command = "fastboot getvar version-bootloader" },
        new() { Category = "Query", Label = "Baseband version",    Command = "fastboot getvar version-baseband" },
        new() { Category = "Query", Label = "Serial number",       Command = "fastboot getvar serialno" },
        new() { Category = "Query", Label = "Unlock status",       Command = "fastboot getvar unlocked" },
        new() { Category = "Query", Label = "Secure boot",         Command = "fastboot getvar secure" },
        new() { Category = "Query", Label = "Current slot",        Command = "fastboot getvar current-slot" },
        new() { Category = "Query", Label = "Slot count",          Command = "fastboot getvar slot-count" },
        new() { Category = "Query", Label = "Max download size",   Command = "fastboot getvar max-download-size" },
        new() { Category = "Query", Label = "Battery voltage",     Command = "fastboot getvar battery-voltage" },
        new() { Category = "Query", Label = "Battery SoC OK",      Command = "fastboot getvar battery-soc-ok" },
        new() { Category = "Query", Label = "Partition type:boot", Command = "fastboot getvar partition-type:boot" },
        new() { Category = "Query", Label = "Partition size:boot", Command = "fastboot getvar partition-size:boot" },
        new() { Category = "Query", Label = "Is userspace?",       Command = "fastboot getvar is-userspace" },
        new() { Category = "Query", Label = "Is logical:system",   Command = "fastboot getvar is-logical:system" },
        new() { Category = "Query", Label = "All variables",       Command = "fastboot getvar all",       Description = "Dump every fastboot variable" },

        // ═══ BOOT ═══
        new() { Category = "Boot", Label = "Reboot normal",        Command = "fastboot reboot" },
        new() { Category = "Boot", Label = "Reboot to bootloader", Command = "fastboot reboot-bootloader" },
        new() { Category = "Boot", Label = "Reboot to recovery",   Command = "fastboot reboot recovery" },
        new() { Category = "Boot", Label = "Reboot to fastbootd",  Command = "fastboot reboot fastboot" },
        new() { Category = "Boot", Label = "Continue boot",        Command = "fastboot continue" },
        new() { Category = "Boot", Label = "Power off (OEM)",      Command = "fastboot oem poweroff" },

        // ═══ SLOTS ═══
        new() { Category = "Slots", Label = "Slot A successful",   Command = "fastboot getvar slot-successful:a" },
        new() { Category = "Slots", Label = "Slot B successful",   Command = "fastboot getvar slot-successful:b" },
        new() { Category = "Slots", Label = "Slot A unbootable",   Command = "fastboot getvar slot-unbootable:a" },
        new() { Category = "Slots", Label = "Slot B unbootable",   Command = "fastboot getvar slot-unbootable:b" },
        new() { Category = "Slots", Label = "Set active slot A",   Command = "fastboot set_active a",     Description = "⚠ Changes boot slot" },
        new() { Category = "Slots", Label = "Set active slot B",   Command = "fastboot set_active b",     Description = "⚠ Changes boot slot" },

        // ═══ OEM ═══
        new() { Category = "OEM", Label = "Device info",           Command = "fastboot oem device-info" },
        new() { Category = "OEM", Label = "OEM unlock",            Command = "fastboot oem unlock",       Description = "⚠ DANGEROUS - wipes data" },
        new() { Category = "OEM", Label = "OEM lock",              Command = "fastboot oem lock" },
        new() { Category = "OEM", Label = "Reboot recovery",       Command = "fastboot oem reboot-recovery" },
        new() { Category = "OEM", Label = "EDL mode",              Command = "fastboot oem edl",          Description = "⚠ Qualcomm only" },

        // ═══ UNLOCK (Google) ═══
        new() { Category = "Unlock", Label = "Get unlock ability", Command = "fastboot flashing get_unlock_ability" },
        new() { Category = "Unlock", Label = "Unlock bootloader",  Command = "fastboot flashing unlock",  Description = "⚠ DANGEROUS - wipes data" },
        new() { Category = "Unlock", Label = "Lock bootloader",    Command = "fastboot flashing lock" },

        // ═══ PARTITION INFO (READ-ONLY) ═══
        new() { Category = "Partitions", Label = "Type of boot",   Command = "fastboot getvar partition-type:boot" },
        new() { Category = "Partitions", Label = "Type of system", Command = "fastboot getvar partition-type:system" },
        new() { Category = "Partitions", Label = "Type of userdata", Command = "fastboot getvar partition-type:userdata" },
        new() { Category = "Partitions", Label = "Size of boot",   Command = "fastboot getvar partition-size:boot" },
        new() { Category = "Partitions", Label = "Size of recovery", Command = "fastboot getvar partition-size:recovery" },
        new() { Category = "Partitions", Label = "Size of userdata", Command = "fastboot getvar partition-size:userdata" },

        // ═══ ADVANCED ═══
        new() { Category = "Advanced", Label = "Re-connect",       Command = "fastboot reconnect" },
        new() { Category = "Advanced", Label = "Reboot bootloader",Command = "fastboot reboot-bootloader" },
        new() { Category = "Advanced", Label = "Flash warning",    Command = "fastboot getvar warning", Description = "Not supported on all devices" }
    };
}
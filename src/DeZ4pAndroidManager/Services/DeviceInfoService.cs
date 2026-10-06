// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Fetches detailed device properties via ADB - OEM-agnostic,
/// Android 7 → 16, with fallbacks for every data source.
/// </summary>
public class DeviceInfoService
{
    private readonly AdbService _adb;

    public DeviceInfoService(AdbService adb) => _adb = adb;

    public async Task<DeviceInfoModel> GetAsync(string serial, CancellationToken ct = default)
    {
        var info = new DeviceInfoModel { Serial = serial };

        // ── Batch 1: base info ──
        var t = new[]
        {
            _adb.ShellAsync(serial, "getprop ro.product.model", ct),
            _adb.ShellAsync(serial, "getprop ro.build.version.release", ct),
            _adb.ShellAsync(serial, "getprop ro.product.cpu.abi", ct),
            _adb.ShellAsync(serial, "dumpsys battery", ct),
            _adb.ShellAsync(serial, "df -k 2>/dev/null || df", ct),
            _adb.ShellAsync(serial, "cat /proc/meminfo", ct),
            _adb.ShellAsync(serial, "getprop ro.build.date", ct),
            _adb.ShellAsync(serial, "getprop ro.build.version.security_patch", ct),
            _adb.ShellAsync(serial, "cat /proc/uptime", ct),
            _adb.ShellAsync(serial, "uname -r", ct),
        };
        try { await Task.WhenAll(t); } catch { }

        info.Model = CleanupText(t[0].Result);
        info.AndroidVersion = CleanupText(t[1].Result);
        info.CpuAbi = CleanupText(t[2].Result);
        ParseBattery(t[3].Result, info);
        ParseStorageFromDfAll(t[4].Result, info);
        ParseMemory(t[5].Result, info);
        info.BuildDate = CleanupText(t[6].Result);
        info.SecurityPatch = CleanupText(t[7].Result);
        info.Uptime = FormatUptime(t[8].Result);
        info.KernelVersion = CleanupText(t[9].Result);

        // ── Batch 2: advanced info ──
        var adv = new[]
        {
            // [0] BEST source: vbmeta.device_state (Android 8+, most reliable)
            _adb.ShellAsync(serial, "getprop ro.boot.vbmeta.device_state", ct),
            // [1] Verified boot state (green/orange/yellow/red)
            _adb.ShellAsync(serial, "getprop ro.boot.verifiedbootstate", ct),
            // [2] Legacy lock state
            _adb.ShellAsync(serial, "getprop ro.secureboot.lockstate", ct),
            // [3] Flash locked flag (unreliable on Xiaomi)
            _adb.ShellAsync(serial, "getprop ro.boot.flash.locked", ct),
            // [4] Verity mode
            _adb.ShellAsync(serial, "getprop ro.boot.veritymode", ct),
            // [5] Root detection
            _adb.ShellAsync(serial, "which su 2>/dev/null || ls /system/xbin/su /system/bin/su /sbin/su 2>/dev/null || echo NOTROOT", ct),
            // [6] SELinux
            _adb.ShellAsync(serial, "getenforce", ct),
            // [7] WiFi info
            _adb.ShellAsync(serial, "dumpsys wifi | grep -m1 'mWifiInfo'", ct),
            // [8] WiFi IP
            _adb.ShellAsync(serial, "ip route | grep wlan", ct),
            // [9] Screen size
            _adb.ShellAsync(serial, "wm size", ct),
            // [10] Screen density
            _adb.ShellAsync(serial, "wm density", ct),
            // [11] CPU info
            _adb.ShellAsync(serial, "cat /proc/cpuinfo | grep -m1 'Hardware\\|model name\\|Processor'", ct),
            // [12] CPU load avg
            _adb.ShellAsync(serial, "cat /proc/loadavg", ct),
            // [13] CPU cores - /sys present
            _adb.ShellAsync(serial, "cat /sys/devices/system/cpu/present 2>/dev/null", ct),
            // [14] CPU cores - nproc
            _adb.ShellAsync(serial, "nproc 2>/dev/null", ct),
            // [15] CPU cores - grep
            _adb.ShellAsync(serial, "grep -c processor /proc/cpuinfo 2>/dev/null", ct),
            // [16] Board name
            _adb.ShellAsync(serial, "getprop ro.product.board", ct),
        };
        try { await Task.WhenAll(adv); } catch { }

        ParseBootloader(adv[0].Result, adv[1].Result, adv[2].Result, adv[3].Result, adv[4].Result, info);
        ParseRoot(adv[5].Result, info);
        ParseSelinux(adv[6].Result, info);
        ParseNetwork(adv[7].Result, adv[8].Result, info);
        ParseDisplay(adv[9].Result, adv[10].Result, info);
        ParseCpuModel(adv[11].Result, adv[16].Result, info);
        ParseCpuLoad(adv[12].Result, info);
        ParseCpuCores(adv[13].Result, adv[14].Result, adv[15].Result, info);

        // Storage fallbacks
        if (info.StorageTotalGb < 0)
        {
            var d = await _adb.ShellAsync(serial, "dumpsys diskstats", ct);
            ParseStorageFromDiskstats(d, info);
        }
        if (info.StorageTotalGb < 0)
        {
            var s = await _adb.ShellAsync(serial, "stat -f /data", ct);
            ParseStorageFromStat(s, info);
        }

        return info;
    }

    // ─── Helpers ───
    private static string CleanupText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "?";
        return s.Replace("\r", "").Trim().Split('\n').Last().Trim();
    }

    private static string[] NormalizeLines(string output)
        => (output ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Select(l => l.TrimEnd()).ToArray();

    // ─── Battery ───
    private static void ParseBattery(string output, DeviceInfoModel info)
    {
        if (string.IsNullOrEmpty(output)) return;
        var m = Regex.Match(output, @"level:\s*(\d+)");
        if (m.Success) info.BatteryLevel = int.Parse(m.Groups[1].Value);

        m = Regex.Match(output, @"health:\s*(\d+)");
        if (m.Success)
            info.BatteryHealth = int.Parse(m.Groups[1].Value) switch
            {
                1 => "Unknown", 2 => "Good", 3 => "Overheat", 4 => "Dead",
                5 => "Over voltage", 6 => "Failure", 7 => "Cold", _ => "Unknown"
            };

        m = Regex.Match(output, @"temperature:\s*(\d+)");
        if (m.Success) info.BatteryTempC = int.Parse(m.Groups[1].Value) / 10.0;

        m = Regex.Match(output, @"voltage:\s*(\d+)");
        if (m.Success) info.BatteryVoltageMv = int.Parse(m.Groups[1].Value);

        m = Regex.Match(output, @"status:\s*(\d+)");
        if (m.Success)
            info.BatteryStatus = int.Parse(m.Groups[1].Value) switch
            {
                1 => "Unknown", 2 => "Charging", 3 => "Discharging",
                4 => "Not charging", 5 => "Full", _ => "Unknown"
            };
    }

    // ─── Bootloader - 5 sources, priority-based ───
    //
    // Priority 1: ro.boot.vbmeta.device_state → "unlocked" / "locked" (BEST, Android 8+)
    // Priority 2: ro.secureboot.lockstate → "unlocked" / "locked" (some OEMs)
    // Priority 3: ro.boot.verifiedbootstate → orange/red = unlocked, green = locked
    // Priority 4: ro.boot.flash.locked → 0 = unlocked, 1 = locked (unreliable on Xiaomi)
    // Priority 5: ro.boot.veritymode → "enforcing" / "disabled" (weak signal)
    //
    private static void ParseBootloader(
        string vbmeta, string vbs, string lockstate, string flashLocked, string verityMode,
        DeviceInfoModel info)
    {
        string vb = (vbmeta ?? "").Trim().ToLowerInvariant();
        string vs = (vbs ?? "").Trim().ToLowerInvariant();
        string ls = (lockstate ?? "").Trim().ToLowerInvariant();
        string fl = (flashLocked ?? "").Trim();
        string vm = (verityMode ?? "").Trim().ToLowerInvariant();

        // P1 - vbmeta.device_state (most reliable on Android 8+)
        if (vb == "unlocked") { info.BootloaderState = "Unlocked"; }
        else if (vb == "locked") { info.BootloaderState = "Locked"; }

        // P2 - secureboot.lockstate
        if (info.BootloaderState == "N/A")
        {
            if (ls == "unlocked") info.BootloaderState = "Unlocked";
            else if (ls == "locked") info.BootloaderState = "Locked";
        }

        // P3 - verifiedbootstate
        if (info.BootloaderState == "N/A")
        {
            if (vs == "orange") info.BootloaderState = "Unlocked";
            else if (vs == "red") info.BootloaderState = "Unlocked";
            else if (vs == "yellow") info.BootloaderState = "Locked (custom key)";
            else if (vs == "green") info.BootloaderState = "Locked";
        }

        // P4 - flash.locked (least reliable)
        if (info.BootloaderState == "N/A")
        {
            if (fl == "0") info.BootloaderState = "Unlocked";
            else if (fl == "1") info.BootloaderState = "Locked";
        }

        // ─── Verified Boot - derived from best available source ───
        // We want to always show something meaningful, not N/A.
        if (!string.IsNullOrEmpty(vs) && vs != "?")
        {
            info.VerifiedBoot = vs switch
            {
                "green" => "Green (verified)",
                "orange" => "Orange (unlocked)",
                "yellow" => "Yellow (custom key)",
                "red" => "Red (failed)",
                _ => vs
            };
        }
        else if (!string.IsNullOrEmpty(vb) && vb != "?")
        {
            // Derive from vbmeta.device_state
            info.VerifiedBoot = vb == "unlocked" ? "Orange (unlocked)"
                                : vb == "locked" ? "Green (verified)"
                                : "N/A";
        }
        else if (!string.IsNullOrEmpty(vm) && vm != "?")
        {
            info.VerifiedBoot = vm switch
            {
                "enforcing" => "Green (verified)",
                "disabled" => "Orange (unlocked)",
                "logging" => "Yellow (logging)",
                _ => "N/A"
            };
        }
        else
        {
            info.VerifiedBoot = "N/A";
        }
    }

    // ─── Root ───
    private static void ParseRoot(string output, DeviceInfoModel info)
    {
        if (string.IsNullOrWhiteSpace(output)) { info.RootStatus = "Not rooted"; return; }
        var o = output.Trim();
        if (o.Contains("NOTROOT") || o.Contains("not found") || o.Contains("No such file"))
        {
            info.RootStatus = "Not rooted";
            return;
        }
        if (Regex.IsMatch(o, @"(^|\s)/(system|sbin|su)/.+"))
        {
            info.RootStatus = "Rooted";
            return;
        }
        info.RootStatus = "Not rooted";
    }

    // ─── SELinux ───
    private static void ParseSelinux(string output, DeviceInfoModel info)
    {
        var se = (output ?? "").Trim();
        info.SelinuxStatus = se switch
        {
            "Enforcing" => "Enforcing",
            "Permissive" => "Permissive",
            _ => "N/A"
        };
    }

    // ─── Network ───
    private static void ParseNetwork(string wifiInfo, string ipRoute, DeviceInfoModel info)
    {
        var ssidM = Regex.Match(wifiInfo ?? "", @"SSID:\s*([^,]+)");
        if (ssidM.Success)
        {
            var ssid = ssidM.Groups[1].Value.Trim().Trim('"');
            if (!string.IsNullOrEmpty(ssid) && ssid != "<unknown ssid>")
                info.WifiSsid = ssid;
        }

        var rssiM = Regex.Match(wifiInfo ?? "", @"RSSI:\s*(-?\d+)");
        if (rssiM.Success) info.WifiRssi = int.Parse(rssiM.Groups[1].Value);

        var ipM = Regex.Match(ipRoute ?? "", @"src\s+(\d+\.\d+\.\d+\.\d+)");
        if (ipM.Success) info.WifiIp = ipM.Groups[1].Value;

        info.ConnectionType = info.WifiIp != "N/A" ? "USB + WiFi" : "USB";
    }

    // ─── Display ───
    private static void ParseDisplay(string sizeOut, string densityOut, DeviceInfoModel info)
    {
        var m = Regex.Match(sizeOut ?? "", @"(\d+)x(\d+)");
        if (m.Success)
            info.ScreenResolution = m.Groups[1].Value + "x" + m.Groups[2].Value;

        var d = Regex.Match(densityOut ?? "", @"(\d+)");
        if (d.Success) info.ScreenDensity = int.Parse(d.Groups[1].Value);

        if (m.Success && d.Success
            && int.TryParse(m.Groups[1].Value, out int w)
            && int.TryParse(m.Groups[2].Value, out int h)
            && int.TryParse(d.Groups[1].Value, out int dpi) && dpi > 0)
        {
            double diagPx = Math.Sqrt((double)w * w + (double)h * h);
            info.ScreenInches = diagPx / dpi;
        }
    }

    // ─── CPU Model ───
    private static void ParseCpuModel(string cpuInfo, string board, DeviceInfoModel info)
    {
        var m = Regex.Match(cpuInfo ?? "", @"(?:Hardware|model name|Processor)\s*:\s*(.+)");
        if (m.Success)
        {
            var value = m.Groups[1].Value.Trim();
            if (!value.StartsWith("AArch64") && !value.StartsWith("ARMv"))
            {
                info.CpuModel = value;
                return;
            }
        }

        var b = (board ?? "").Trim();
        if (!string.IsNullOrEmpty(b) && b != "?")
        {
            info.CpuModel = b;
            return;
        }

        info.CpuModel = "N/A";
    }

    // ─── CPU Load ───
    private static void ParseCpuLoad(string loadavg, DeviceInfoModel info)
    {
        var m = Regex.Match(loadavg ?? "", @"([\d.]+)\s+");
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double load1))
        {
            info.CpuLoadPercent = Math.Min(100, load1 * 25);
        }
    }

    // ─── CPU Cores ───
    private static void ParseCpuCores(string presentRaw, string nprocRaw, string cpuInfoCountRaw, DeviceInfoModel info)
    {
        var present = (presentRaw ?? "").Trim();
        if (!string.IsNullOrEmpty(present) && !present.Contains("No such"))
        {
            int total = CountFromCpuRange(present);
            if (total > 0) { info.CpuCores = total; return; }
        }

        var nproc = (nprocRaw ?? "").Trim();
        if (int.TryParse(nproc, out int n) && n > 0)
        {
            info.CpuCores = n;
            return;
        }

        var cnt = (cpuInfoCountRaw ?? "").Trim();
        if (int.TryParse(cnt, out int c) && c > 0)
        {
            info.CpuCores = c;
            return;
        }

        info.CpuCores = -1;
    }

    private static int CountFromCpuRange(string range)
    {
        int total = 0;
        foreach (var segment in range.Split(','))
        {
            var s = segment.Trim();
            if (string.IsNullOrEmpty(s)) continue;

            if (s.Contains('-'))
            {
                var parts = s.Split('-');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int start)
                    && int.TryParse(parts[1], out int end) && end >= start)
                {
                    total += (end - start + 1);
                }
            }
            else if (int.TryParse(s, out _))
            {
                total += 1;
            }
        }
        return total;
    }

    // ─── Storage ───
    private static void ParseStorageFromDfAll(string output, DeviceInfoModel info)
    {
        if (string.IsNullOrWhiteSpace(output)) return;
        var lines = NormalizeLines(output);

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (raw.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            string mount = parts[^1];

            if (mount == "/data")
            {
                ParseDfRow(parts, out double total, out double used, out double free, out int pct);
                if (total > 0)
                {
                    info.StorageTotalGb = total; info.StorageUsedGb = used;
                    info.StorageFreeGb = free; info.StorageUsedPercent = pct;
                }
            }
            else if (mount.StartsWith("/storage/", StringComparison.Ordinal)
                     && !mount.StartsWith("/storage/emulated", StringComparison.Ordinal)
                     && !mount.StartsWith("/storage/self", StringComparison.Ordinal)
                     && !mount.StartsWith("/storage/enc_emulated", StringComparison.Ordinal))
            {
                ParseDfRow(parts, out double total, out double used, out double free, out int pct);
                if (total > 0)
                {
                    info.SdCardDetected = true;
                    info.SdCardPath = mount;
                    info.SdCardTotalGb = total; info.SdCardUsedGb = used;
                    info.SdCardFreeGb = free; info.SdCardUsedPercent = pct;
                }
            }
        }
    }

    private static void ParseDfRow(string[] parts, out double totalGb, out double usedGb, out double freeGb, out int pct)
    {
        totalGb = usedGb = freeGb = -1; pct = -1;

        var pctM = Regex.Match(parts[^2], @"(\d+)");
        if (pctM.Success) pct = int.Parse(pctM.Groups[1].Value);

        if (long.TryParse(parts[^5], out long tKb) &&
            long.TryParse(parts[^4], out long uKb) &&
            long.TryParse(parts[^3], out long aKb))
        {
            totalGb = tKb / 1024.0 / 1024.0;
            usedGb = uKb / 1024.0 / 1024.0;
            freeGb = aKb / 1024.0 / 1024.0;
            return;
        }

        totalGb = ParseHumanSizeGb(parts[^5]);
        usedGb = ParseHumanSizeGb(parts[^4]);
        freeGb = ParseHumanSizeGb(parts[^3]);
    }

    private static double ParseHumanSizeGb(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return -1;
        s = s.Trim().ToUpperInvariant();
        double mult;
        if (s.EndsWith("T")) { mult = 1024.0; s = s[..^1]; }
        else if (s.EndsWith("G")) { mult = 1.0; s = s[..^1]; }
        else if (s.EndsWith("M")) { mult = 1.0 / 1024.0; s = s[..^1]; }
        else if (s.EndsWith("K")) { mult = 1.0 / (1024.0 * 1024.0); s = s[..^1]; }
        else if (s.EndsWith("B")) { mult = 1.0 / (1024.0 * 1024.0 * 1024.0); s = s[..^1]; }
        else return -1;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v * mult : -1;
    }

    private static void ParseStorageFromDiskstats(string output, DeviceInfoModel info)
    {
        if (string.IsNullOrWhiteSpace(output)) return;
        var m = Regex.Match(output, @"DataSize[=:]\s*(\d+)");
        var f = Regex.Match(output, @"DataFree[=:]\s*(\d+)");
        if (m.Success)
        {
            long tb = long.Parse(m.Groups[1].Value);
            info.StorageTotalGb = tb / 1024.0 / 1024.0 / 1024.0;
            if (f.Success)
            {
                long fb = long.Parse(f.Groups[1].Value);
                info.StorageFreeGb = fb / 1024.0 / 1024.0 / 1024.0;
                info.StorageUsedGb = info.StorageTotalGb - info.StorageFreeGb;
                if (info.StorageTotalGb > 0)
                    info.StorageUsedPercent = (int)Math.Round(info.StorageUsedGb / info.StorageTotalGb * 100);
            }
        }
    }

    private static void ParseStorageFromStat(string output, DeviceInfoModel info)
    {
        if (string.IsNullOrWhiteSpace(output)) return;
        var bs = Regex.Match(output, @"Fundamental block size:\s*(\d+)");
        var bl = Regex.Match(output, @"Blocks:\s*Total:\s*(\d+)\s+Free:\s*(\d+)\s+Available:\s*(\d+)");
        if (!bs.Success || !bl.Success) return;
        if (!long.TryParse(bs.Groups[1].Value, out long bs2)) return;
        if (!long.TryParse(bl.Groups[1].Value, out long tot)) return;
        if (!long.TryParse(bl.Groups[3].Value, out long avail)) return;
        long tb = tot * bs2, fb = avail * bs2, ub = tb - fb;
        info.StorageTotalGb = tb / 1024.0 / 1024.0 / 1024.0;
        info.StorageUsedGb = ub / 1024.0 / 1024.0 / 1024.0;
        info.StorageFreeGb = fb / 1024.0 / 1024.0 / 1024.0;
        if (info.StorageTotalGb > 0)
            info.StorageUsedPercent = (int)Math.Round(info.StorageUsedGb / info.StorageTotalGb * 100);
    }

    private static void ParseMemory(string output, DeviceInfoModel info)
    {
        if (string.IsNullOrEmpty(output)) return;
        long R(string k)
        {
            var m = Regex.Match(output, $@"{k}:\s*(\d+)");
            return m.Success ? long.Parse(m.Groups[1].Value) : 0;
        }
        var total = R("MemTotal"); var avail = R("MemAvailable");
        var st = R("SwapTotal"); var sf = R("SwapFree");
        if (total > 0)
        {
            info.RamTotalGb = total / 1024.0 / 1024.0;
            if (avail > 0)
            {
                info.RamFreeGb = avail / 1024.0 / 1024.0;
                info.RamUsedGb = (total - avail) / 1024.0 / 1024.0;
            }
        }
        if (st > 0)
        {
            info.SwapTotalGb = st / 1024.0 / 1024.0;
            info.SwapFreeGb = sf / 1024.0 / 1024.0;
            info.SwapUsedGb = (st - sf) / 1024.0 / 1024.0;
        }
    }

    private static string FormatUptime(string output)
    {
        if (string.IsNullOrEmpty(output)) return "?";
        var first = output.Trim().Split(' ')[0];
        if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) return "?";
        var ts = TimeSpan.FromSeconds(s);
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{(int)ts.TotalMinutes}m";
    }
}
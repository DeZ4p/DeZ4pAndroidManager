// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class AnalyticsService
{
    private readonly AdbService _adb;

    private readonly Dictionary<string, (DateTime ts, BatteryDeep data)> _batteryCache = new();
    private readonly Dictionary<string, (DateTime ts, List<StorageCategory> data)> _storageCache = new();
    private readonly Dictionary<string, (DateTime ts, List<TopApp> data)> _topAppsCache = new();
    private readonly object _lock = new();

    private static readonly TimeSpan CacheValidity = TimeSpan.FromSeconds(120);

    private static readonly string DebugLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeZ4pAndroidManager");
    private static readonly string DebugLogPath = Path.Combine(DebugLogDir, "storage_debug.log");

    public AnalyticsService(AdbService adb) => _adb = adb;

    public void ClearCache()
    {
        lock (_lock) { _batteryCache.Clear(); _storageCache.Clear(); _topAppsCache.Clear(); }
    }

    public bool TryGetCachedBattery(string serial, out BatteryDeep? value)
    {
        lock (_lock)
            if (_batteryCache.TryGetValue(serial, out var c) && DateTime.UtcNow - c.ts < CacheValidity)
            { value = c.data; return true; }
        value = null; return false;
    }

    public bool TryGetCachedStorage(string serial, out List<StorageCategory>? value)
    {
        lock (_lock)
            if (_storageCache.TryGetValue(serial, out var c) && DateTime.UtcNow - c.ts < CacheValidity)
            { value = c.data; return true; }
        value = null; return false;
    }

    public bool TryGetCachedTopApps(string serial, out List<TopApp>? value)
    {
        lock (_lock)
            if (_topAppsCache.TryGetValue(serial, out var c) && DateTime.UtcNow - c.ts < CacheValidity)
            { value = c.data; return true; }
        value = null; return false;
    }

    // ═══════════════════════════════════════════════════════════
    //  STORAGE - try 6 methods
    // ═══════════════════════════════════════════════════════════
    public async Task<List<StorageCategory>> GetStorageBreakdownAsync(string serial, CancellationToken ct = default)
    {
        if (TryGetCachedStorage(serial, out var cached) && cached != null)
            return cached;

        var log = new System.Text.StringBuilder();
        log.AppendLine($"=== STORAGE DEBUG {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        log.AppendLine($"Serial: {serial}");
        log.AppendLine();

        double totalGb = await GetTotalStorageGbAsync(serial, log, ct);

        var entries = new List<(string Name, double Gb)>();

        // ─── Method 1: du -sk with explicit base path (no wildcard) ───
        entries = await TryMethod1_DuBase(serial, log, ct);
        if (entries.Count > 0) log.AppendLine($"✅ Method 1 worked: {entries.Count} entries");
        else log.AppendLine("❌ Method 1 failed");

        // ─── Method 2: find + du per folder ───
        if (entries.Count == 0)
        {
            entries = await TryMethod2_FindDu(serial, log, ct);
            if (entries.Count > 0) log.AppendLine($"✅ Method 2 worked: {entries.Count} entries");
            else log.AppendLine("❌ Method 2 failed");
        }

        // ─── Method 3: ls -la to get folder sizes (only shows counts) ───
        if (entries.Count == 0)
        {
            entries = await TryMethod3_FindSize(serial, log, ct);
            if (entries.Count > 0) log.AppendLine($"✅ Method 3 worked: {entries.Count} entries");
            else log.AppendLine("❌ Method 3 failed");
        }

        // ─── Method 4: stat -f fallback ───
        if (entries.Count == 0)
        {
            entries = await TryMethod4_Toybox(serial, log, ct);
            if (entries.Count > 0) log.AppendLine($"✅ Method 4 worked: {entries.Count} entries");
            else log.AppendLine("❌ Method 4 failed");
        }

        // ─── Method 5: busybox if available ───
        if (entries.Count == 0)
        {
            entries = await TryMethod5_Busybox(serial, log, ct);
            if (entries.Count > 0) log.AppendLine($"✅ Method 5 worked: {entries.Count} entries");
            else log.AppendLine("❌ Method 5 failed");
        }

        // ─── Method 6: content query MediaStore ───
        if (entries.Count == 0)
        {
            entries = await TryMethod6_MediaStore(serial, log, ct);
            if (entries.Count > 0) log.AppendLine($"✅ Method 6 worked: {entries.Count} entries");
            else log.AppendLine("❌ Method 6 failed");
        }

        // Write log
        try
        {
            Directory.CreateDirectory(DebugLogDir);
            File.WriteAllText(DebugLogPath, log.ToString(), System.Text.UTF8Encoding.UTF8);
        }
        catch { }

        var list = entries
            .OrderByDescending(e => e.Gb)
            .Take(15)
            .Select(e => new StorageCategory
            {
                Icon = GetFolderIcon(e.Name),
                Name = e.Name,
                SizeGb = e.Gb,
                PercentOfTotal = totalGb > 0 ? Math.Min(100, e.Gb / totalGb * 100) : 0
            })
            .ToList();

        lock (_lock) _storageCache[serial] = (DateTime.UtcNow, list);
        return list;
    }

    // ─── Total storage ───
    private async Task<double> GetTotalStorageGbAsync(string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        try
        {
            var df = await _adb.ShellAsync(serial, "df -k /data 2>&1", ct);
            log.AppendLine("=== df -k /data ===");
            log.AppendLine(df);
            log.AppendLine();

            var m = Regex.Match(df ?? "", @"^.*?\s+(\d+)\s+\d+\s+\d+\s+\d+%\s+/data",
                RegexOptions.Multiline);
            if (m.Success && long.TryParse(m.Groups[1].Value, out long tKb))
            {
                double total = tKb / 1024.0 / 1024.0;
                log.AppendLine($"totalGb: {total}");
                return total;
            }
        }
        catch (Exception ex) { log.AppendLine($"df error: {ex.Message}"); }
        return -1;
    }

    // ═══════════════════════════════════════════════════════════
    //  METHOD 1: du -sk /storage/emulated/0 (no subfolders)
    // ═══════════════════════════════════════════════════════════
    private async Task<List<(string Name, double Gb)>> TryMethod1_DuBase(
        string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        var result = new List<(string, double)>();
        try
        {
            log.AppendLine();
            log.AppendLine("=== Method 1: du -sk /storage/emulated/0/* (no 2>/dev/null) ===");

            var cmd = "du -sk /storage/emulated/0/* 2>&1";
            var raw = await _adb.ShellAsync(serial, cmd, ct);

            log.AppendLine("Raw output:");
            log.AppendLine(raw);
            log.AppendLine();

            result = ParseDuOutput(raw);
        }
        catch (Exception ex) { log.AppendLine($"M1 error: {ex.Message}"); }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  METHOD 2: find + wc + manual size
    // ═══════════════════════════════════════════════════════════
    private async Task<List<(string Name, double Gb)>> TryMethod2_FindDu(
        string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        var result = new List<(string, double)>();
        try
        {
            log.AppendLine();
            log.AppendLine("=== Method 2: find + awk for folder sizes ===");

            // Single command that runs find/awk on all top-level folders
            var cmd = "cd /storage/emulated/0 2>/dev/null && for d in */; do s=$(find \"$d\" -type f -exec stat -c %s {} + 2>/dev/null | awk '{n+=$1} END {print n}'); [ -n \"$s\" ] && [ \"$s\" != \"0\" ] && echo \"$d $s\"; done";

            var raw = await _adb.ShellAsync(serial, cmd, ct);

            log.AppendLine("Raw output (first 40 lines):");
            var lines = (raw ?? "").Split('\n').Take(40);
            foreach (var l in lines) log.AppendLine(l);
            log.AppendLine();

            foreach (var line in (raw ?? "").Replace("\r", "").Split('\n'))
            {
                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var name = parts[0].TrimEnd('/');
                if (string.IsNullOrEmpty(name) || name.StartsWith(".")) continue;
                if (!long.TryParse(parts[1], out long bytes)) continue;

                double gb = bytes / 1024.0 / 1024.0 / 1024.0;
                if (gb < 0.005) continue;

                result.Add((name, gb));
            }
        }
        catch (Exception ex) { log.AppendLine($"M2 error: {ex.Message}"); }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  METHOD 3: find with size output directly
    // ═══════════════════════════════════════════════════════════
    private async Task<List<(string Name, double Gb)>> TryMethod3_FindSize(
        string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        var result = new List<(string, double)>();
        try
        {
            log.AppendLine();
            log.AppendLine("=== Method 3: find -type f -printf (per folder) ===");

            var folders = new[] { "DCIM", "Pictures", "Movies", "Download", "Downloads",
                                  "Music", "Documents", "Android", "WhatsApp", "Telegram",
                                  "Screenshots", "Recordings", "Backups", "TWRP", "Video" };

            // Build one combined command
            var cmds = new List<string>();
            foreach (var f in folders)
            {
                cmds.Add($"c=$(find /storage/emulated/0/{f} -type f 2>/dev/null | wc -l); " +
                         $"[ \"$c\" != \"0\" ] && echo \"{f}=$c\"");
            }

            var cmd = string.Join("; ", cmds);
            var raw = await _adb.ShellAsync(serial, cmd, ct);

            log.AppendLine("Raw output:");
            log.AppendLine(raw);
            log.AppendLine();
            log.AppendLine("(Note: this returns FILE COUNTS, not sizes)");

            // We can't get real sizes this way, so this method is informational
            // If we reach here, the previous methods have failed
        }
        catch (Exception ex) { log.AppendLine($"M3 error: {ex.Message}"); }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  METHOD 4: toybox du explicitly
    // ═══════════════════════════════════════════════════════════
    private async Task<List<(string Name, double Gb)>> TryMethod4_Toybox(
        string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        var result = new List<(string, double)>();
        try
        {
            log.AppendLine();
            log.AppendLine("=== Method 4: toybox du ===");

            var cmd = "toybox du -sk /storage/emulated/0/* 2>&1";
            var raw = await _adb.ShellAsync(serial, cmd, ct);

            log.AppendLine("Raw output:");
            log.AppendLine(raw);

            result = ParseDuOutput(raw);
        }
        catch (Exception ex) { log.AppendLine($"M4 error: {ex.Message}"); }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  METHOD 5: busybox
    // ═══════════════════════════════════════════════════════════
    private async Task<List<(string Name, double Gb)>> TryMethod5_Busybox(
        string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        var result = new List<(string, double)>();
        try
        {
            log.AppendLine();
            log.AppendLine("=== Method 5: busybox du ===");

            // Test if busybox exists
            var check = await _adb.ShellAsync(serial, "which busybox 2>&1", ct);
            log.AppendLine($"busybox path: {check}");

            if (string.IsNullOrWhiteSpace(check) || check.Contains("not found"))
            {
                log.AppendLine("busybox not available");
                return result;
            }

            var cmd = "busybox du -sk /storage/emulated/0/* 2>&1";
            var raw = await _adb.ShellAsync(serial, cmd, ct);

            log.AppendLine("Raw output:");
            log.AppendLine(raw);

            result = ParseDuOutput(raw);
        }
        catch (Exception ex) { log.AppendLine($"M5 error: {ex.Message}"); }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  METHOD 6: MediaStore query
    // ═══════════════════════════════════════════════════════════
    private async Task<List<(string Name, double Gb)>> TryMethod6_MediaStore(
        string serial, System.Text.StringBuilder log, CancellationToken ct)
    {
        var result = new List<(string, double)>();
        try
        {
            log.AppendLine();
            log.AppendLine("=== Method 6: MediaStore via content query ===");

            // Query images total
            var q1 = await _adb.ShellAsync(serial,
                "content query --uri content://media/external/images/media --projection _size 2>&1 | grep _size | awk -F= '{n+=$2} END {print n}'",
                ct);
            var q2 = await _adb.ShellAsync(serial,
                "content query --uri content://media/external/video/media --projection _size 2>&1 | grep _size | awk -F= '{n+=$2} END {print n}'",
                ct);
            var q3 = await _adb.ShellAsync(serial,
                "content query --uri content://media/external/audio/media --projection _size 2>&1 | grep _size | awk -F= '{n+=$2} END {print n}'",
                ct);
            var q4 = await _adb.ShellAsync(serial,
                "content query --uri content://media/external/file --projection _size 2>&1 | grep _size | awk -F= '{n+=$2} END {print n}'",
                ct);

            log.AppendLine($"Images bytes: {q1.Trim()}");
            log.AppendLine($"Videos bytes: {q2.Trim()}");
            log.AppendLine($"Audio bytes:  {q3.Trim()}");
            log.AppendLine($"Files bytes:  {q4.Trim()}");

            if (long.TryParse(q1.Trim(), out long imgBytes) && imgBytes > 0)
                result.Add(("Images", imgBytes / 1024.0 / 1024.0 / 1024.0));
            if (long.TryParse(q2.Trim(), out long vidBytes) && vidBytes > 0)
                result.Add(("Videos", vidBytes / 1024.0 / 1024.0 / 1024.0));
            if (long.TryParse(q3.Trim(), out long audBytes) && audBytes > 0)
                result.Add(("Audio", audBytes / 1024.0 / 1024.0 / 1024.0));
            if (long.TryParse(q4.Trim(), out long fileBytes) && fileBytes > 0)
                result.Add(("Files", fileBytes / 1024.0 / 1024.0 / 1024.0));
        }
        catch (Exception ex) { log.AppendLine($"M6 error: {ex.Message}"); }
        return result;
    }

    private static List<(string Name, double Gb)> ParseDuOutput(string output)
    {
        var result = new List<(string Name, double Gb)>();
        if (string.IsNullOrWhiteSpace(output)) return result;

        foreach (var rawLine in output.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("du:")) continue;
            if (line.StartsWith("ls:")) continue;
            if (line.StartsWith("toybox:")) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            if (!long.TryParse(parts[0], out long kb)) continue;

            var fullPath = parts[1].TrimEnd('/');
            var name = fullPath.Contains('/')
                ? fullPath.Substring(fullPath.LastIndexOf('/') + 1)
                : fullPath;

            if (string.IsNullOrEmpty(name) || name.StartsWith(".")) continue;

            double gb = kb / 1024.0 / 1024.0;
            if (gb < 0.005) continue;

            result.Add((name, gb));
        }

        return result
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Gb).First())
            .ToList();
    }

    private static string GetFolderIcon(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("image") || n.Contains("photo") || n.Contains("dcim") || n.Contains("picture")) return "IMG";
        if (n.Contains("movie") || n.Contains("video")) return "VID";
        if (n.Contains("music") || n.Contains("audio") || n.Contains("podcast")) return "AUD";
        if (n.Contains("file")) return "DIR";
        if (n.Contains("download")) return "DL";
        if (n.Contains("document")) return "DOC";
        return "DIR";
    }

    // ═══════════════════════════════════════════════════════════
    //  BATTERY
    // ═══════════════════════════════════════════════════════════
    public async Task<BatteryDeep> GetBatteryDeepAsync(string serial, CancellationToken ct = default)
    {
        if (TryGetCachedBattery(serial, out var cached) && cached != null)
            return cached;

        var b = new BatteryDeep();

        var t = new[]
        {
            _adb.ShellAsync(serial, "cat /sys/class/power_supply/battery/cycle_count 2>/dev/null", ct),
            _adb.ShellAsync(serial, "cat /sys/class/power_supply/battery/charge_full_design 2>/dev/null", ct),
            _adb.ShellAsync(serial, "cat /sys/class/power_supply/battery/charge_full 2>/dev/null", ct),
            _adb.ShellAsync(serial, "cat /sys/class/power_supply/battery/uevent 2>/dev/null | grep -i 'POWER_SUPPLY_CHARGE_FULL\\|CYCLE'", ct),
        };
        try { await Task.WhenAll(t); } catch { }

        if (int.TryParse(t[0].Result.Trim(), out int cycles) && cycles > 0)
            b.CycleCount = cycles;

        if (long.TryParse(t[1].Result.Trim(), out long designUah) && designUah > 100000)
            b.DesignCapacityMah = designUah / 1000.0;
        else if (long.TryParse(t[1].Result.Trim(), out long designMah) && designMah > 100)
            b.DesignCapacityMah = designMah;

        if (long.TryParse(t[2].Result.Trim(), out long curUah) && curUah > 100000)
            b.CurrentCapacityMah = curUah / 1000.0;
        else if (long.TryParse(t[2].Result.Trim(), out long curMah) && curMah > 100)
            b.CurrentCapacityMah = curMah;

        if (b.DesignCapacityMah > 0 && b.CurrentCapacityMah > 0)
        {
            double pct = b.CurrentCapacityMah / b.DesignCapacityMah * 100.0;
            pct = Math.Max(0, Math.Min(120, pct));
            b.HealthPercent = $"{pct:0}%";
        }

        if (b.CycleCount > 0)
        {
            double days = b.CycleCount / 1.5;
            b.AgeText = days > 365 ? $"~{days / 365.0:0.0} years" : $"~{days:0} days";
        }

        lock (_lock) _batteryCache[serial] = (DateTime.UtcNow, b);
        return b;
    }

    // ═══════════════════════════════════════════════════════════
    //  TOP APPS
    // ═══════════════════════════════════════════════════════════
    public async Task<List<TopApp>> GetTopAppsAsync(string serial, CancellationToken ct = default)
    {
        if (TryGetCachedTopApps(serial, out var cached) && cached != null)
            return cached;

        var cpuTask = _adb.ShellAsync(serial, "dumpsys cpuinfo 2>/dev/null | head -60", ct);
        var memTask = _adb.ShellAsync(serial,
            "dumpsys meminfo 2>/dev/null | sed -n '/Total PSS by process/,/^$/p' | head -60", ct);

        try { await Task.WhenAll(cpuTask, memTask); } catch { }

        var cpuMap = ParseCpuMap(cpuTask.Result);
        var ramMap = ParseRamMap(memTask.Result);

        var keys = new HashSet<string>(cpuMap.Keys);
        keys.UnionWith(ramMap.Keys);

        var apps = new List<TopApp>();
        foreach (var name in keys)
        {
            var cpu = cpuMap.TryGetValue(name, out double c) ? c : 0;
            var ram = ramMap.TryGetValue(name, out double r) ? r : 0;
            double score = cpu + ram * 0.04;
            if (score < 0.5) continue;

            apps.Add(new TopApp { Name = name, CpuPercent = cpu, MemMb = ram, Score = score });
        }

        var result = apps.OrderByDescending(a => a.Score).Take(15).ToList();
        lock (_lock) _topAppsCache[serial] = (DateTime.UtcNow, result);
        return result;
    }

    private static Dictionary<string, double> ParseCpuMap(string output)
    {
        var map = new Dictionary<string, double>();
        foreach (var line in (output ?? "").Replace("\r", "").Split('\n'))
        {
            var m = Regex.Match(line, @"^\s*([\d.]+)%\s+\d+/([^\s:]+)");
            if (!m.Success) continue;

            var name = m.Groups[2].Value;
            if (name.StartsWith("system_server") || name.StartsWith("/")
                || name.StartsWith("kworker") || name.StartsWith("ksoftirq")) continue;

            var shortName = ShortenName(name);
            if (string.IsNullOrEmpty(shortName)) continue;

            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double pct))
            {
                if (!map.TryGetValue(shortName, out double existing) || pct > existing)
                    map[shortName] = pct;
            }
        }
        return map;
    }

    private static Dictionary<string, double> ParseRamMap(string output)
    {
        var map = new Dictionary<string, double>();
        foreach (var line in (output ?? "").Replace("\r", "").Split('\n'))
        {
            var m = Regex.Match(line, @"^\s*([\d,]+)K:\s+(\S+)\s+\(pid");
            if (!m.Success) continue;

            var name = m.Groups[2].Value;
            if (name.StartsWith("system_server") || name.StartsWith("/")) continue;

            var shortName = ShortenName(name);
            if (string.IsNullOrEmpty(shortName)) continue;

            if (long.TryParse(m.Groups[1].Value.Replace(",", ""), out long kb))
            {
                double mb = kb / 1024.0;
                if (!map.TryGetValue(shortName, out double existing) || mb > existing)
                    map[shortName] = mb;
            }
        }
        return map;
    }

    private static string ShortenName(string pkg)
    {
        int idx = pkg.IndexOf(':');
        if (idx > 0) pkg = pkg.Substring(0, idx);

        var parts = pkg.Split('.');
        if (parts.Length >= 2)
        {
            var last = parts[^1];
            var secondLast = parts[^2];
            if (last.Length <= 3 && secondLast.Length > 0)
                return $"{secondLast}.{last}";
            return last;
        }
        return pkg;
    }

    public static (int score, string grade, string color) CalculateHealthScore(
        int batteryLevel, string batteryHealth,
        int storageUsedPct, double ramUsedPct,
        double batteryTempC, int cycleCount)
    {
        int score = 100;
        if (batteryHealth == "Overheat" || batteryHealth == "Dead" || batteryHealth == "Failure") score -= 20;
        else if (batteryHealth == "Cold") score -= 10;

        if (storageUsedPct > 95) score -= 25;
        else if (storageUsedPct > 90) score -= 15;
        else if (storageUsedPct > 80) score -= 8;

        if (ramUsedPct > 95) score -= 20;
        else if (ramUsedPct > 85) score -= 10;
        else if (ramUsedPct > 75) score -= 5;

        if (batteryTempC > 45) score -= 20;
        else if (batteryTempC > 40) score -= 10;
        else if (batteryTempC > 38) score -= 5;

        if (cycleCount > 1000) score -= 15;
        else if (cycleCount > 500) score -= 8;
        else if (cycleCount > 300) score -= 4;

        score = Math.Max(0, Math.Min(100, score));

        string grade = score switch
        {
            >= 90 => "Excellent",
            >= 75 => "Good",
            >= 60 => "Fair",
            >= 40 => "Poor",
            _ => "Critical"
        };
        string color = score switch
        {
            >= 90 => "#34D399",
            >= 75 => "#22D3EE",
            >= 60 => "#F59E0B",
            >= 40 => "#F97316",
            _ => "#EF4444"
        };
        return (score, grade, color);
    }
}
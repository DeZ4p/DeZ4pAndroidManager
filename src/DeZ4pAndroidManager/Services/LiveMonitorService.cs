// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Samples live device metrics (CPU, RAM, network, thermal zones).
/// Uses /proc files - universal across Android 7 → 16.
/// </summary>
public class LiveMonitorService
{
    private readonly AdbService _adb;
    private long _prevIdle, _prevTotal;
    private long _prevRx, _prevTx;
    private DateTime _prevTime = DateTime.MinValue;

    public LiveMonitorService(AdbService adb) => _adb = adb;

    public void Reset()
    {
        _prevIdle = _prevTotal = _prevRx = _prevTx = 0;
        _prevTime = DateTime.MinValue;
    }

    public async Task SampleAsync(string serial, LiveResult result, CancellationToken ct = default)
    {
        var t = new[]
        {
            _adb.ShellAsync(serial, "cat /proc/stat | head -1", ct),
            _adb.ShellAsync(serial, "grep -E 'MemTotal|MemAvailable' /proc/meminfo", ct),
            _adb.ShellAsync(serial, "cat /proc/net/dev", ct),
            _adb.ShellAsync(serial, "for i in 0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do t=$(cat /sys/class/thermal/thermal_zone$i/type 2>/dev/null); v=$(cat /sys/class/thermal/thermal_zone$i/temp 2>/dev/null); [ -n \"$t\" ] && echo \"$t=$v\"; done", ct),
            _adb.ShellAsync(serial, "cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor 2>/dev/null", ct),
        };
        try { await Task.WhenAll(t); } catch { }

        ParseCpu(t[0].Result, result);
        ParseRam(t[1].Result, result);
        ParseNet(t[2].Result, result);
        ParseThermal(t[3].Result, result);
        ParseGovernor(t[4].Result, result);
    }

    private void ParseCpu(string output, LiveResult r)
    {
        var m = Regex.Match(output ?? "", @"cpu\s+(.+)");
        if (!m.Success) return;

        var parts = m.Groups[1].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 7) return;

        long[] v = new long[parts.Length];
        for (int i = 0; i < parts.Length; i++) long.TryParse(parts[i], out v[i]);

        long idle = v[3] + (v.Length > 4 ? v[4] : 0);
        long total = v.Sum();

        if (_prevTotal > 0 && total > _prevTotal)
        {
            long dt = total - _prevTotal;
            long di = idle - _prevIdle;
            r.CpuPercent = dt > 0 ? Math.Max(0, Math.Min(100, (1.0 - (double)di / dt) * 100.0)) : 0;
        }
        _prevTotal = total;
        _prevIdle = idle;
    }

    private static void ParseRam(string output, LiveResult r)
    {
        var tot = Regex.Match(output ?? "", @"MemTotal:\s*(\d+)");
        var av = Regex.Match(output ?? "", @"MemAvailable:\s*(\d+)");
        if (!tot.Success) return;

        long tKb = long.Parse(tot.Groups[1].Value);
        long aKb = av.Success ? long.Parse(av.Groups[1].Value) : 0;

        r.RamTotalGb = tKb / 1024.0 / 1024.0;
        if (aKb > 0)
        {
            r.RamUsedGb = (tKb - aKb) / 1024.0 / 1024.0;
            r.RamPercent = r.RamTotalGb > 0 ? r.RamUsedGb / r.RamTotalGb * 100.0 : 0;
        }
    }

    private void ParseNet(string output, LiveResult r)
    {
        long rx = 0, tx = 0;
        foreach (var line in (output ?? "").Replace("\r", "").Split('\n'))
        {
            if (!line.Contains(':')) continue;
            var idx = line.IndexOf(':');
            if (line.Substring(0, idx).Trim() == "lo") continue;
            var vals = line.Substring(idx + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (vals.Length < 9) continue;
            if (long.TryParse(vals[0], out long a)) rx += a;
            if (long.TryParse(vals[8], out long b)) tx += b;
        }

        var now = DateTime.UtcNow;
        if (_prevTime != DateTime.MinValue)
        {
            double dt = (now - _prevTime).TotalSeconds;
            if (dt > 0)
            {
                long dRx = Math.Max(0, rx - _prevRx);
                long dTx = Math.Max(0, tx - _prevTx);
                r.NetDownKbps = dRx / 1024.0 / dt;
                r.NetUpKbps = dTx / 1024.0 / dt;
            }
        }
        _prevRx = rx; _prevTx = tx; _prevTime = now;
    }

    private static void ParseThermal(string output, LiveResult r)
    {
        r.ThermalZones.Clear();
        if (string.IsNullOrWhiteSpace(output)) return;

        foreach (var line in output.Replace("\r", "").Split('\n'))
        {
            var parts = line.Split('=');
            if (parts.Length != 2) continue;

            string name = parts[0].Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double mv)) continue;

            // Ignore zones with invalid temps
            double temp = mv / 1000.0;
            if (temp < -50 || temp > 200) continue;

            // Clean up name
            name = name.Replace("_", " ").Trim();
            if (name.Length > 20) name = name.Substring(0, 20);

            r.ThermalZones.Add(new ThermalZone { Name = name, TempC = temp });
        }
    }

    private static void ParseGovernor(string output, LiveResult r)
    {
        var g = (output ?? "").Trim();
        r.CpuGovernor = string.IsNullOrEmpty(g) ? "N/A" : g;
    }
}

/// <summary>
/// Holds a single live sample result.
/// </summary>
public class LiveResult
{
    public double CpuPercent { get; set; }
    public double RamPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double NetUpKbps { get; set; }
    public double NetDownKbps { get; set; }
    public string CpuGovernor { get; set; } = "N/A";
    public ObservableCollection<ThermalZone> ThermalZones { get; } = new();
}
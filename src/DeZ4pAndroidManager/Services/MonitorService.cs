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
/// Real-time device resource sampler. Uses /proc files (fast and universal).
/// CPU usage computed via /proc/stat delta; RAM via /proc/meminfo;
/// network via /proc/net/dev delta.
/// </summary>
public class MonitorService
{
    private readonly AdbService _adb;

    // CPU state
    private long _prevIdle, _prevTotal;
    private DateTime _prevCpuTime = DateTime.MinValue;

    // Network state
    private long _prevRx, _prevTx;
    private DateTime _prevNetTime = DateTime.MinValue;

    public MonitorService(AdbService adb) => _adb = adb;

    public async Task<MonitorSample> SampleAsync(string serial, CancellationToken ct = default)
    {
        var s = new MonitorSample();

        var t = new[]
        {
            _adb.ShellAsync(serial, "cat /proc/stat | head -1", ct),
            _adb.ShellAsync(serial, "grep -E 'MemTotal|MemAvailable' /proc/meminfo", ct),
            _adb.ShellAsync(serial, "cat /proc/net/dev", ct),
            _adb.ShellAsync(serial, "cat /proc/loadavg", ct),
        };
        try { await Task.WhenAll(t); } catch { }

        ParseCpu(t[0].Result, s);
        ParseRam(t[1].Result, s);
        ParseNet(t[2].Result, s);
        ParseLoad(t[3].Result, s);

        return s;
    }

    /// <summary>Resets delta state (call when switching devices).</summary>
    public void Reset()
    {
        _prevIdle = _prevTotal = 0;
        _prevRx = _prevTx = 0;
        _prevCpuTime = DateTime.MinValue;
        _prevNetTime = DateTime.MinValue;
    }

    // ─── CPU ───
    private void ParseCpu(string output, MonitorSample s)
    {
        if (string.IsNullOrWhiteSpace(output)) return;

        var m = Regex.Match(output, @"cpu\s+(.+)");
        if (!m.Success) return;

        var parts = m.Groups[1].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 7) return;

        long[] vals = new long[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            long.TryParse(parts[i], out vals[i]);

        long idle = vals[3] + (vals.Length > 4 ? vals[4] : 0); // idle + iowait
        long total = vals.Sum();

        if (_prevTotal > 0 && total > _prevTotal)
        {
            long dTotal = total - _prevTotal;
            long dIdle = idle - _prevIdle;
            double usage = dTotal > 0 ? (1.0 - (double)dIdle / dTotal) * 100.0 : 0;
            s.CpuPercent = Math.Max(0, Math.Min(100, usage));
        }

        _prevTotal = total;
        _prevIdle = idle;
        _prevCpuTime = DateTime.UtcNow;
    }

    // ─── RAM ───
    private static void ParseRam(string output, MonitorSample s)
    {
        if (string.IsNullOrWhiteSpace(output)) return;

        var totalM = Regex.Match(output, @"MemTotal:\s*(\d+)");
        var availM = Regex.Match(output, @"MemAvailable:\s*(\d+)");

        if (!totalM.Success) return;
        long totalKb = long.Parse(totalM.Groups[1].Value);
        long availKb = availM.Success ? long.Parse(availM.Groups[1].Value) : 0;

        s.RamTotalGb = totalKb / 1024.0 / 1024.0;

        if (availKb > 0)
        {
            s.RamUsedGb = (totalKb - availKb) / 1024.0 / 1024.0;
            s.RamPercent = s.RamTotalGb > 0 ? s.RamUsedGb / s.RamTotalGb * 100.0 : 0;
        }
    }

    // ─── Network ───
    private void ParseNet(string output, MonitorSample s)
    {
        if (string.IsNullOrWhiteSpace(output)) return;

        long rx = 0, tx = 0;
        foreach (var line in output.Replace("\r", "").Split('\n'))
        {
            if (!line.Contains(':')) continue;
            var idx = line.IndexOf(':');
            var iface = line.Substring(0, idx).Trim();
            if (iface == "lo") continue;

            var vals = line.Substring(idx + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (vals.Length < 9) continue;

            if (long.TryParse(vals[0], out long r)) rx += r;
            if (long.TryParse(vals[8], out long t2)) tx += t2;
        }

        var now = DateTime.UtcNow;
        if (_prevNetTime != DateTime.MinValue)
        {
            double dt = (now - _prevNetTime).TotalSeconds;
            if (dt > 0)
            {
                long dRx = rx - _prevRx;
                long dTx = tx - _prevTx;
                if (dRx < 0) dRx = 0;
                if (dTx < 0) dTx = 0;
                s.NetDownKbps = dRx / 1024.0 / dt;
                s.NetUpKbps = dTx / 1024.0 / dt;
            }
        }

        _prevRx = rx;
        _prevTx = tx;
        _prevNetTime = now;
    }

    // ─── Load average ───
    private static void ParseLoad(string output, MonitorSample s)
    {
        var m = Regex.Match(output ?? "", @"([\d.]+)\s+");
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double load))
        {
            s.LoadAvg1 = load;
        }
    }
}
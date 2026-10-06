// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class SensorService
{
    private readonly AdbService _adb;

    public SensorService(AdbService adb) => _adb = adb;

    /// <summary>Last raw output collected (used for diagnostics in the UI).</summary>
    public string LastRawDump { get; private set; } = "";

    /// <summary>Which strategy succeeded ("dumpsys", "sysfs", "getprop", "none").</summary>
    public string LastStrategy { get; private set; } = "";

    /// <summary>Diagnostic message when parsing fails or partially succeeds.</summary>
    public string LastDiagnostic { get; private set; } = "";

    public async Task<List<SensorInfo>> ListAsync(string serial, CancellationToken ct = default)
    {
        var diag = new System.Text.StringBuilder();
        var raw = new System.Text.StringBuilder();

        // ═══ Strategy 1: dumpsys sensorservice ═══
        diag.AppendLine("▶ Strategy 1: dumpsys sensorservice");
        string dump = "";
        try
        {
            dump = await _adb.ShellAsync(serial, "dumpsys sensorservice 2>&1", ct) ?? "";
            raw.AppendLine("═══ dumpsys sensorservice ═══");
            raw.AppendLine(dump);
        }
        catch (Exception ex) { diag.AppendLine($"  err: {ex.Message}"); }

        if (!string.IsNullOrWhiteSpace(dump) &&
            !dump.Contains("Permission Denial", StringComparison.OrdinalIgnoreCase) &&
            !dump.Contains("Unknown service", StringComparison.OrdinalIgnoreCase))
        {
            var sensors = ParseDumpsys(dump);
            diag.AppendLine($"  parsed: {sensors.Count}");
            if (sensors.Count > 0)
            {
                LastRawDump = raw.ToString();
                LastStrategy = "dumpsys";
                LastDiagnostic = diag.ToString();
                return sensors;
            }
        }
        else
        {
            diag.AppendLine($"  empty or permission denied");
        }

        // ═══ Strategy 2: /sys/class/sensors ═══
        diag.AppendLine("▶ Strategy 2: /sys/class/sensors");
        try
        {
            var sysfsRaw = await _adb.ShellAsync(serial,
                "ls /sys/class/sensors/ 2>&1", ct) ?? "";
            raw.AppendLine("═══ /sys/class/sensors/ listing ═══");
            raw.AppendLine(sysfsRaw);

            if (!sysfsRaw.Contains("No such file") && !sysfsRaw.Contains("Permission denied"))
            {
                var sensors = await ParseSysfsAsync(serial, sysfsRaw, raw, ct);
                diag.AppendLine($"  parsed: {sensors.Count}");
                if (sensors.Count > 0)
                {
                    LastRawDump = raw.ToString();
                    LastStrategy = "sysfs";
                    LastDiagnostic = diag.ToString();
                    return sensors;
                }
            }
            else diag.AppendLine("  path not available");
        }
        catch (Exception ex) { diag.AppendLine($"  err: {ex.Message}"); }

        // ═══ Strategy 3: getprop (fallback - limited info) ═══
        diag.AppendLine("▶ Strategy 3: getprop");
        try
        {
            var props = await _adb.ShellAsync(serial,
                "getprop | grep -iE 'sensor|accelerometer|gyroscope|proximity|magnetometer' 2>&1", ct) ?? "";
            raw.AppendLine("═══ getprop (sensor-related) ═══");
            raw.AppendLine(props);
            diag.AppendLine($"  props: {(string.IsNullOrWhiteSpace(props) ? "none" : "found")}");
        }
        catch (Exception ex) { diag.AppendLine($"  err: {ex.Message}"); }

        // ═══ Nothing worked ═══
        LastRawDump = raw.ToString();
        LastStrategy = "none";
        LastDiagnostic = diag.ToString() +
            "\n\nIf the raw dump shows sensor info but the parser misses it, send it so we can adapt.";

        return new List<SensorInfo>();
    }

    // ═══════════════════════════════════════════════════════════
    //  Strategy 1: parse dumpsys (multiple formats)
    // ═══════════════════════════════════════════════════════════
    private static List<SensorInfo> ParseDumpsys(string dump)
    {
        var list = new List<SensorInfo>();
        if (string.IsNullOrWhiteSpace(dump)) return list;

        var lines = dump.Replace("\r", "").Split('\n');

        // Pattern A: "0xHHHHHHHH) Name | Vendor | version=N | type=..."
        var patA = new Regex(
            @"^0x([0-9a-fA-F]+)\)\s+(.+?)\s*\|\s*(.+?)\s*\|\s*version=(\d+)\s*\|\s*type=(.+?)(?:\s*\||\s*$)",
            RegexOptions.IgnoreCase);

        // Pattern B: "0xHHHHHHHH) Name - Vendor - version N - type ..."
        var patB = new Regex(
            @"^0x([0-9a-fA-F]+)\)\s+(.+?)\s+-\s+(.+?)\s+-\s+version\s+(\d+)\s+-\s+type\s+(.+?)(?:\s+-|\s*$)",
            RegexOptions.IgnoreCase);

        // Pattern C (older): "0xHHHHHHHH) Name (Vendor) type: ... version: N"
        var patC = new Regex(
            @"^0x([0-9a-fA-F]+)\)\s+(.+?)\s*\(([^)]+)\)\s*type:\s*(.+?)\s*version:\s*(\d+)",
            RegexOptions.IgnoreCase);

        foreach (var rawLine in lines)
        {
            var t = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) continue;

            // Try A
            var mA = patA.Match(t);
            if (mA.Success)
            {
                var info = BuildSensor(
                    handle: Convert.ToInt32(mA.Groups[1].Value, 16),
                    name: mA.Groups[2].Value.Trim(),
                    vendor: mA.Groups[3].Value.Trim(),
                    version: int.TryParse(mA.Groups[4].Value, out var v) ? v : 0,
                    typeStr: mA.Groups[5].Value.Trim(),
                    tail: t.Substring(mA.Index + mA.Length),
                    rawLine: t);
                list.Add(info);
                continue;
            }

            // Try B
            var mB = patB.Match(t);
            if (mB.Success)
            {
                var info = BuildSensor(
                    handle: Convert.ToInt32(mB.Groups[1].Value, 16),
                    name: mB.Groups[2].Value.Trim(),
                    vendor: mB.Groups[3].Value.Trim(),
                    version: int.TryParse(mB.Groups[4].Value, out var v) ? v : 0,
                    typeStr: mB.Groups[5].Value.Trim(),
                    tail: t.Substring(mB.Index + mB.Length),
                    rawLine: t);
                list.Add(info);
                continue;
            }

            // Try C
            var mC = patC.Match(t);
            if (mC.Success)
            {
                var info = BuildSensor(
                    handle: Convert.ToInt32(mC.Groups[1].Value, 16),
                    name: mC.Groups[2].Value.Trim(),
                    vendor: mC.Groups[3].Value.Trim(),
                    version: int.TryParse(mC.Groups[5].Value, out var v) ? v : 0,
                    typeStr: mC.Groups[4].Value.Trim(),
                    tail: t.Substring(mC.Index + mC.Length),
                    rawLine: t);
                list.Add(info);
                continue;
            }

            // Loose: extract at least handle + first name chunk
            var loose = Regex.Match(t, @"^0x([0-9a-fA-F]+)\)\s+(.+?)(?:\s*\||\s*$)");
            if (loose.Success)
            {
                var name = loose.Groups[2].Value.Trim();
                var info = new SensorInfo
                {
                    Handle = Convert.ToInt32(loose.Groups[1].Value, 16),
                    Name = name,
                    Vendor = "-",
                    Version = 0,
                    TypeName = GuessTypeFromName(name),
                    TypeId = 0,
                    RawLine = t
                };
                list.Add(info);
            }
        }

        return list;
    }

    private static SensorInfo BuildSensor(int handle, string name, string vendor, int version,
                                           string typeStr, string tail, string rawLine)
    {
        var info = new SensorInfo
        {
            Handle = handle,
            Name = name,
            Vendor = vendor,
            Version = version,
            RawLine = rawLine
        };

        // type may be "android.sensor.accelerometer(1)" or "1"
        var tm = Regex.Match(typeStr, @"^(.+?)\((\d+)\)\s*$");
        if (tm.Success)
        {
            info.TypeName = tm.Groups[1].Value.Trim();
            info.TypeId = int.TryParse(tm.Groups[2].Value, out var tid) ? tid : 0;
        }
        else if (int.TryParse(typeStr, out var tid2))
        {
            info.TypeId = tid2;
            info.TypeName = $"android.sensor.sensor_{tid2}";
        }
        else
        {
            info.TypeName = typeStr;
        }

        // Parse tail fields
        var mr = Regex.Match(tail, @"maxRange=([\d\.Ee\+\-]+)");
        if (mr.Success) info.MaxRange = mr.Groups[1].Value;

        var res = Regex.Match(tail, @"resolution=([\d\.Ee\+\-]+)");
        if (res.Success) info.Resolution = res.Groups[1].Value;

        var pw = Regex.Match(tail, @"power=([\d\.Ee\+\-]+)");
        if (pw.Success) info.Power = pw.Groups[1].Value;

        var md = Regex.Match(tail, @"minDelay=(\d+)");
        if (md.Success) info.MinDelay = md.Groups[1].Value;

        var fm = Regex.Match(tail, @"fifoMax(?:EventCount)?=(\d+)");
        if (fm.Success) info.FifoMax = fm.Groups[1].Value;

        return info;
    }

    // ═══════════════════════════════════════════════════════════
    //  Strategy 2: parse /sys/class/sensors/
    // ═══════════════════════════════════════════════════════════
    private async Task<List<SensorInfo>> ParseSysfsAsync(
        string serial, string listing, System.Text.StringBuilder raw, CancellationToken ct)
    {
        var result = new List<SensorInfo>();
        var names = listing.Replace("\r", "").Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Contains(":"))
            .ToList();

        int idx = 0;
        foreach (var name in names)
        {
            ct.ThrowIfCancellationRequested();

            string sensorName = name;
            string vendor = "-";

            try
            {
                var nameRaw = await _adb.ShellAsync(serial,
                    $"cat /sys/class/sensors/{name}/name 2>/dev/null", ct);
                if (!string.IsNullOrWhiteSpace(nameRaw)) sensorName = nameRaw.Trim();

                var vendorRaw = await _adb.ShellAsync(serial,
                    $"cat /sys/class/sensors/{name}/vendor 2>/dev/null", ct);
                if (!string.IsNullOrWhiteSpace(vendorRaw)) vendor = vendorRaw.Trim();
            }
            catch { }

            result.Add(new SensorInfo
            {
                Handle = idx++,
                Name = sensorName,
                Vendor = vendor,
                Version = 1,
                TypeName = GuessTypeFromName(sensorName),
                TypeId = 0,
                RawLine = $"/sys/class/sensors/{name}"
            });
        }

        raw.AppendLine("═══ /sys/class/sensors/ details ═══");
        foreach (var s in result)
            raw.AppendLine($"  {s.Name}  ({s.Vendor})");

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  Type detection from name
    // ═══════════════════════════════════════════════════════════
    private static string GuessTypeFromName(string name)
    {
        var n = (name ?? "").ToLowerInvariant();
        if (n.Contains("accel")) return "android.sensor.accelerometer";
        if (n.Contains("gyro")) return "android.sensor.gyroscope";
        if (n.Contains("magnet") || n.Contains("compass")) return "android.sensor.magnetic_field";
        if (n.Contains("proximity") || n.Contains("prox")) return "android.sensor.proximity";
        if (n.Contains("light") || n.Contains("als")) return "android.sensor.light";
        if (n.Contains("pressure") || n.Contains("baro")) return "android.sensor.pressure";
        if (n.Contains("step") && n.Contains("detect")) return "android.sensor.step_detector";
        if (n.Contains("step")) return "android.sensor.step_counter";
        if (n.Contains("heart")) return "android.sensor.heart_rate";
        if (n.Contains("gravity")) return "android.sensor.gravity";
        if (n.Contains("linear")) return "android.sensor.linear_acceleration";
        if (n.Contains("rotation") && n.Contains("game")) return "android.sensor.game_rotation_vector";
        if (n.Contains("rotation")) return "android.sensor.rotation_vector";
        if (n.Contains("orientation")) return "android.sensor.orientation";
        if (n.Contains("temp")) return "android.sensor.ambient_temperature";
        if (n.Contains("humid")) return "android.sensor.relative_humidity";
        if (n.Contains("hall")) return "android.sensor.hall";
        if (n.Contains("hinge")) return "android.sensor.hinge_angle";
        if (n.Contains("significant") || n.Contains("sig_motion")) return "android.sensor.significant_motion";
        return "android.sensor.unknown";
    }

    /// <summary>Loads the raw dump only (used by a separate button in the UI).</summary>
    public async Task<string> GetRawDumpAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ShellAsync(serial, "dumpsys sensorservice 2>&1", ct);
            return r ?? "(empty)";
        }
        catch (Exception ex) { return $"error: {ex.Message}"; }
    }
}
// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class BatteryLabService
{
    private readonly AdbService _adb;
    public BatteryLabService(AdbService adb) => _adb = adb;

    public async Task<BatteryInfo> GetAsync(string serial, CancellationToken ct = default)
    {
        var info = new BatteryInfo();

        var t1 = _adb.ShellAsync(serial, "dumpsys battery 2>/dev/null", ct);
        var t2 = _adb.ShellAsync(serial, "cat /sys/class/power_supply/battery/uevent 2>/dev/null", ct);
        var t3 = _adb.ShellAsync(serial, "dumpsys batterystats --charged 2>/dev/null | head -40", ct);
        try { await Task.WhenAll(t1, t2, t3); } catch { }

        ParseDumpsys(t1.Result ?? "", info);
        ParseUevent(t2.Result ?? "", info);
        ParseBattStats(t3.Result ?? "", info);

        return info;
    }

    private static void ParseDumpsys(string raw, BatteryInfo info)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        int I(string key, int def = -1)
        {
            var m = Regex.Match(raw, $@"\b{key}:\s*(-?\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : def;
        }
        var level = I("level");
        if (level >= 0) info.Level = level;

        var health = I("health");
        if (health >= 0) info.Health = health switch
        {
            1 => "Unknown", 2 => "Good", 3 => "Overheat",
            4 => "Dead", 5 => "Over voltage", 6 => "Failure", 7 => "Cold", _ => "Unknown"
        };

        var status = I("status");
        if (status >= 0) info.Status = status switch
        {
            1 => "Unknown", 2 => "Charging", 3 => "Discharging",
            4 => "Not charging", 5 => "Full", _ => "Unknown"
        };

        var temp = I("temperature");
        if (temp > 0) info.TemperatureC = temp / 10.0;

        var volt = I("voltage");
        if (volt > 0) info.VoltageMv = volt;

        var tech = Regex.Match(raw, @"\btechnology:\s*(\S+)");
        if (tech.Success) info.Technology = tech.Groups[1].Value;

        var source = Regex.Match(raw, @"\bAC powered:\s*(true|false)");
        var usb = Regex.Match(raw, @"\bUSB powered:\s*(true|false)");
        var wireless = Regex.Match(raw, @"\bWireless powered:\s*(true|false)");
        var ac = source.Success && source.Groups[1].Value == "true";
        var u = usb.Success && usb.Groups[1].Value == "true";
        var w = wireless.Success && wireless.Groups[1].Value == "true";
        info.ChargeSource = ac ? "AC Adapter" : u ? "USB" : w ? "Wireless" : "Battery";
    }

    private static void ParseUevent(string raw, BatteryInfo info)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        string V(string key)
        {
            var m = Regex.Match(raw, $@"(?:^|\n)POWER_SUPPLY_{Regex.Escape(key)}=(.+)");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }
        int N(string key)
        {
            var v = V(key);
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
        }

        var cur = N("CURRENT_NOW");
        if (cur != -1) info.CurrentMa = cur / 1000;   // ÂµA â†’ mA

        var designUah = N("CHARGE_FULL_DESIGN");
        if (designUah > 100000) info.DesignCapacityMah = designUah / 1000;
        else if (designUah > 100) info.DesignCapacityMah = designUah;

        var curUah = N("CHARGE_FULL");
        if (curUah > 100000) info.CurrentCapacityMah = curUah / 1000;
        else if (curUah > 100) info.CurrentCapacityMah = curUah;

        var cycle = N("CYCLE_COUNT");
        if (cycle >= 0) info.CycleCount = cycle;

        var maxCur = N("CONSTANT_CHARGE_CURRENT_MAX");
        if (maxCur > 0) info.MaxChargingCurrentMa = maxCur / 1000;

        var maxVolt = N("CONSTANT_CHARGE_VOLTAGE_MAX");
        if (maxVolt > 0) info.MaxChargingVoltageMv = maxVolt / 1000;

        if (!string.IsNullOrEmpty(V("TECHNOLOGY"))) info.Technology = V("TECHNOLOGY");
    }

    private static void ParseBattStats(string raw, BatteryInfo info)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var m = Regex.Match(raw, @"Estimated battery capacity:\s*([\d\.]+)\s*mAh");
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var est))
        {
            info.PowerProfile = $"{est:0} mAh (estimated)";
        }
    }
}
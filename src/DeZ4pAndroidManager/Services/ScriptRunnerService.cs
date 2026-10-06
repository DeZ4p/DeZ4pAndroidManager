// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class ScriptResult
{
    public string Command { get; set; } = "";
    public string Output { get; set; } = "";
    public int ExitCode { get; set; }
    public long DurationMs { get; set; }
    public bool Success => ExitCode == 0;
}

public class ScriptRunnerService
{
    private readonly AdbService _adb;
    public ScriptRunnerService(AdbService adb) => _adb = adb;

    public async Task<List<ScriptResult>> RunAsync(
        string serial, ScriptItem script,
        IProgress<(int idx, int total, string cmd)>? progress,
        CancellationToken ct = default)
    {
        var results = new List<ScriptResult>();
        int idx = 0;
        foreach (var cmd in script.Commands)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress?.Report((idx, script.Commands.Count, cmd));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            ConsoleResult r;
            try { r = await _adb.ExecuteRawAsync($"-s {serial} shell {cmd}", 30000, ct)
                       is var raw ? new ConsoleResult { ExitCode = raw.ExitCode, StdOut = raw.StandardOutput ?? "", StdErr = raw.StandardError ?? "" } : new ConsoleResult(); }
            catch (Exception ex) { r = new ConsoleResult { ExitCode = -1, StdErr = ex.Message }; }
            sw.Stop();

            results.Add(new ScriptResult
            {
                Command = cmd,
                Output = string.IsNullOrWhiteSpace(r.StdOut) ? r.StdErr : r.StdOut,
                ExitCode = r.ExitCode,
                DurationMs = sw.ElapsedMilliseconds
            });
        }
        return results;
    }

    public static List<ScriptItem> GetBuiltInScripts()
    {
        var list = new List<ScriptItem>();

        var s1 = new ScriptItem { Name = "Device Health Check", Description = "Quick overview of key device metrics", Category = "Diagnostics", Icon = "\uE95E", Color = "#22D3EE" };
        s1.Commands.Add("getprop ro.product.model");
        s1.Commands.Add("getprop ro.build.version.release");
        s1.Commands.Add("cat /proc/uptime");
        s1.Commands.Add("cat /proc/loadavg");
        s1.Commands.Add("cat /proc/meminfo | head -3");
        s1.Commands.Add("df -h /data");
        s1.Commands.Add("dumpsys battery | grep -E 'level|health|temperature'");
        list.Add(s1);

        var s2 = new ScriptItem { Name = "Network Diagnostics", Description = "Wi-Fi, DNS, and connectivity info", Category = "Network", Icon = "\uE968", Color = "#34D399" };
        s2.Commands.Add("ip addr show wlan0");
        s2.Commands.Add("ip route");
        s2.Commands.Add("getprop net.dns1");
        s2.Commands.Add("ping -c 3 8.8.8.8");
        s2.Commands.Add("ping -c 3 google.com");
        list.Add(s2);

        var s3 = new ScriptItem { Name = "List User Apps", Description = "Show all third-party installed apps", Category = "Apps", Icon = "\uE71D", Color = "#A855F7" };
        s3.Commands.Add("pm list packages -3");
        list.Add(s3);

        var s4 = new ScriptItem { Name = "Storage Report", Description = "Disk usage summary for /sdcard", Category = "Storage", Icon = "\uE8B7", Color = "#F59E0B" };
        s4.Commands.Add("df -h");
        s4.Commands.Add("du -sh /sdcard/* 2>/dev/null | sort -h | tail -10");
        list.Add(s4);

        var s5 = new ScriptItem { Name = "Battery Diagnostic", Description = "Full battery status and history", Category = "Battery", Icon = "\uE83F", Color = "#34D399" };
        s5.Commands.Add("dumpsys battery");
        s5.Commands.Add("dumpsys batterystats --charged | head -30");
        list.Add(s5);

        var s6 = new ScriptItem { Name = "Network Latency Test", Description = "Test ping latency to common hosts", Category = "Network", Icon = "\uE701", Color = "#EC4899" };
        s6.Commands.Add("ping -c 3 1.1.1.1");
        s6.Commands.Add("ping -c 3 8.8.4.4");
        s6.Commands.Add("ping -c 3 208.67.222.222");
        list.Add(s6);

        var s7 = new ScriptItem { Name = "Security Audit", Description = "Bootloader, SELinux, Root status", Category = "Security", Icon = "\uE72E", Color = "#EF4444" };
        s7.Commands.Add("getprop ro.boot.vbmeta.device_state");
        s7.Commands.Add("getprop ro.boot.verifiedbootstate");
        s7.Commands.Add("getenforce");
        s7.Commands.Add("which su");
        list.Add(s7);

        var s8 = new ScriptItem { Name = "Clear All Caches", Description = "Remove app caches on device (needs root for some apps)", Category = "Maintenance", Icon = "\uE74D", Color = "#F97316" };
        s8.Commands.Add("pm trim-caches 99999999G");
        list.Add(s8);

        var s9 = new ScriptItem { Name = "Top CPU Consumers", Description = "Show processes using most CPU", Category = "Diagnostics", Icon = "\uE945", Color = "#FBBF24" };
        s9.Commands.Add("top -n 1 -b -o %CPU,CMDLINE | head -20");
        list.Add(s9);

        var s10 = new ScriptItem { Name = "Bluetooth & Location Status", Description = "Check key system services", Category = "Diagnostics", Icon = "\uE702", Color = "#818CF8" };
        s10.Commands.Add("settings get global bluetooth_on");
        s10.Commands.Add("settings get secure location_mode");
        s10.Commands.Add("settings get global airplane_mode_on");
        list.Add(s10);

        return list;
    }
}
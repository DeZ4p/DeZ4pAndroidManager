// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class ProcessService
{
    private readonly AdbService _adb;

    public ProcessService(AdbService adb) => _adb = adb;

    public async Task<List<ProcessInfo>> ListAsync(string serial, CancellationToken ct = default)
    {
        var list = new List<ProcessInfo>();

        // Try modern toybox format first
        var raw = await _adb.ShellAsync(serial, "ps -A -o USER,PID,PPID,VSZ,RSS,S,NAME 2>/dev/null", ct);

        // Fallback to plain ps -A
        if (string.IsNullOrWhiteSpace(raw) || raw.Contains("Unknown option") || raw.Contains("bad -o"))
        {
            raw = await _adb.ShellAsync(serial, "ps -A 2>/dev/null", ct);
        }

        if (string.IsNullOrWhiteSpace(raw)) return list;

        var lines = raw.Replace("\r", "").Split('\n');
        bool firstLine = true;

        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            var t = line.Trim();
            if (string.IsNullOrEmpty(t)) continue;

            // Skip header
            if (firstLine)
            {
                firstLine = false;
                if (t.StartsWith("USER", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("PID", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 8) continue;

            // Format: USER PID PPID VSZ RSS WCHAN ADDR S NAME [NAME continues...]
            // or: USER PID PPID VSZ RSS S NAME
            try
            {
                var info = new ProcessInfo
                {
                    User = parts[0],
                    FullName = string.Join(" ", parts.Skip(8))
                };

                // Detect format by trying to parse parts[7] as state char
                if (parts[7].Length <= 3 && "SDZTRtWI<>+".Contains(parts[7][0]))
                {
                    // Classic format: [0]=USER [1]=PID [2]=PPID [3]=VSZ [4]=RSS [5]=WCHAN [6]=ADDR [7]=S [8+]=NAME
                    info.Pid = int.TryParse(parts[1], out var pid) ? pid : 0;
                    info.Ppid = int.TryParse(parts[2], out var ppid) ? ppid : 0;
                    info.VszKb = long.TryParse(parts[3], out var vsz) ? vsz : 0;
                    info.RssKb = long.TryParse(parts[4], out var rss) ? rss : 0;
                    info.State = parts[7];
                    info.Name = string.Join(" ", parts.Skip(8));
                }
                else if (parts.Length >= 7)
                {
                    // Modern toybox: [0]=USER [1]=PID [2]=PPID [3]=VSZ [4]=RSS [5]=S [6+]=NAME
                    info.Pid = int.TryParse(parts[1], out var pid) ? pid : 0;
                    info.Ppid = int.TryParse(parts[2], out var ppid) ? ppid : 0;
                    info.VszKb = long.TryParse(parts[3], out var vsz) ? vsz : 0;
                    info.RssKb = long.TryParse(parts[4], out var rss) ? rss : 0;
                    info.State = parts[5];
                    info.Name = string.Join(" ", parts.Skip(6));
                    info.FullName = info.Name;
                }
                else continue;

                if (string.IsNullOrEmpty(info.Name)) info.Name = info.FullName;
                if (info.Pid <= 0) continue;

                list.Add(info);
            }
            catch { }
        }

        return list.OrderByDescending(p => p.RssKb).ToList();
    }

    /// <summary>Kill by PID (needs root or same user).</summary>
    public async Task<(bool ok, string message)> KillAsync(string serial, int pid, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell kill -9 {pid}", 5000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0 && string.IsNullOrEmpty(combined);
            return (ok, ok ? "Killed" : (string.IsNullOrEmpty(combined) ? "Failed" : combined));
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>Force-stop by package name (works without root for user apps).</summary>
    public async Task<(bool ok, string message)> ForceStopAsync(string serial, string package, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell am force-stop {package}", 8000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0;
            return (ok, ok ? $"Stopped {package}" : combined);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
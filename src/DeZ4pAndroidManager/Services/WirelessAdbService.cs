// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeZ4pAndroidManager.Services;

public class WirelessAdbService
{
    private readonly AdbService _adb;

    public WirelessAdbService(AdbService adb) => _adb = adb;

    public async Task<int> GetApiLevelAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ShellAsync(serial, "getprop ro.build.version.sdk", ct);
            return int.TryParse(r?.Trim(), out var api) ? api : 0;
        }
        catch { return 0; }
    }

    public async Task<string> GetAndroidVersionAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ShellAsync(serial, "getprop ro.build.version.release", ct);
            return string.IsNullOrEmpty(r) ? "?" : r.Trim();
        }
        catch { return "?"; }
    }

    public async Task<string> GetDeviceIpAsync(string serial, CancellationToken ct = default)
    {
        var cmds = new[]
        {
            "ip -4 addr show wlan0 2>/dev/null | grep -oE 'inet [0-9.]+' | awk '{print $2}' | head -1",
            "ip addr show wlan0 2>/dev/null | grep 'inet ' | awk '{print $2}' | cut -d/ -f1 | head -1",
            "ifconfig wlan0 2>/dev/null | grep 'inet addr' | awk '{print $2}' | cut -d: -f2",
            "getprop dhcp.wlan0.ipaddress",
            "getprop wifi.interface.ip"
        };

        foreach (var cmd in cmds)
        {
            try
            {
                var r = await _adb.ShellAsync(serial, cmd, ct);
                var ip = r?.Trim();
                if (!string.IsNullOrEmpty(ip) && ip.Contains(".") && !ip.StartsWith("127."))
                    return ip;
            }
            catch { }
        }

        return "";
    }

    public async Task<(bool ok, string message)> EnableTcpIpModeAsync(
        string serial, int port = 5555, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} tcpip {port}", 15000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(combined) ? "OK" : combined);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string message)> ConnectAsync(
        string ip, int port = 5555, CancellationToken ct = default)
    {
        try
        {
            var target = $"{ip}:{port}";
            var r = await _adb.ExecuteRawAsync($"connect {target}", 15000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0 &&
                     (combined.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0);
            return (ok, combined);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string message)> DisconnectAsync(
        string ip, int port = 5555, CancellationToken ct = default)
    {
        try
        {
            var target = $"{ip}:{port}";
            var r = await _adb.ExecuteRawAsync($"disconnect {target}", 10000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, combined);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string message)> PairAsync(
        string ip, int port, string code, CancellationToken ct = default)
    {
        try
        {
            var target = $"{ip}:{port}";
            var r = await _adb.ExecuteRawAsync($"pair {target} {code}", 25000, ct);
            var combined = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0 &&
                     (combined.IndexOf("Successfully", StringComparison.OrdinalIgnoreCase) >= 0);
            return (ok, combined);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<bool> IsTcpIpEnabledAsync(string serial, int port = 5555, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ShellAsync(serial, $"getprop service.adb.tcp.port", ct);
            return r?.Trim() == port.ToString();
        }
        catch { return false; }
    }
}
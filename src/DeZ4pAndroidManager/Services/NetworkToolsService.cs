// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class NetworkToolsService
{
    private readonly AdbService _adb;
    public NetworkToolsService(AdbService adb) => _adb = adb;

    public async Task<NetworkInfo> GetAsync(string serial, CancellationToken ct = default)
    {
        var info = new NetworkInfo();

        var wifi = _adb.ShellAsync(serial, "dumpsys wifi 2>/dev/null | grep -m1 mWifiInfo", ct);
        var ip = _adb.ShellAsync(serial, "ip addr show 2>/dev/null", ct);
        var route = _adb.ShellAsync(serial, "ip route 2>/dev/null", ct);
        var dns = _adb.ShellAsync(serial, "getprop | grep -E 'net\\.dns[0-9]' 2>/dev/null", ct);
        var mob = _adb.ShellAsync(serial, "getprop | grep -E 'gsm\\.(sim\\.operator\\.alpha|network\\.type|operator\\.numeric)' 2>/dev/null", ct);
        var ifstat = _adb.ShellAsync(serial, "cat /proc/net/dev 2>/dev/null", ct);

        try { await Task.WhenAll(wifi, ip, route, dns, mob, ifstat); } catch { }

        ParseWifi(wifi.Result ?? "", info);
        ParseIp(ip.Result ?? "", info);
        ParseRoute(route.Result ?? "", info);
        ParseDns(dns.Result ?? "", info);
        ParseMobile(mob.Result ?? "", info);
        ParseIfstat(ifstat.Result ?? "", info);

        return info;
    }

    private static void ParseWifi(string raw, NetworkInfo info)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var ssid = Regex.Match(raw, @"SSID:\s*""?([^,""]+)""?");
        if (ssid.Success)
        {
            var v = ssid.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(v) && v != "<unknown ssid>") info.WifiSsid = v;
        }
        var rssi = Regex.Match(raw, @"RSSI:\s*(-?\d+)");
        if (rssi.Success && int.TryParse(rssi.Groups[1].Value, out var r)) info.WifiRssi = r;
        var mac = Regex.Match(raw, @"MAC:\s*([0-9a-f:]{17})", RegexOptions.IgnoreCase);
        if (mac.Success) info.WifiMac = mac.Groups[1].Value;
        var link = Regex.Match(raw, @"Link speed:\s*(\d+)");
        if (link.Success && int.TryParse(link.Groups[1].Value, out var ls)) info.WifiLinkSpeedMbps = ls;
        var freq = Regex.Match(raw, @"Frequency:\s*(\d+)");
        if (freq.Success && int.TryParse(freq.Groups[1].Value, out var f)) info.WifiFrequencyMhz = f;
    }

    private static void ParseIp(string raw, NetworkInfo info)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var blocks = Regex.Split(raw.Replace("\r", ""), @"\n\d+:\s");
        foreach (var block in blocks)
        {
            var header = Regex.Match(block, @"^(\S+):");
            var name = header.Success ? header.Groups[1].Value : "";
            var state = Regex.Match(block, @"state\s+(\S+)").Groups[1].Value;
            var inet = Regex.Match(block, @"inet\s+(\d+\.\d+\.\d+\.\d+)");
            var mac = Regex.Match(block, @"link/ether\s+([0-9a-f:]{17})", RegexOptions.IgnoreCase);
            var mtu = Regex.Match(block, @"mtu\s+(\d+)");

            if (string.IsNullOrEmpty(name)) continue;
            if (name == "lo") continue;

            var iface = new NetworkInterfaceInfo
            {
                Name = name,
                State = string.IsNullOrEmpty(state) ? "â€”" : state.ToUpperInvariant(),
                Ip = inet.Success ? inet.Groups[1].Value : "â€”",
                Mac = mac.Success ? mac.Groups[1].Value : "â€”",
                Mtu = mtu.Success ? mtu.Groups[1].Value : "â€”"
            };
            info.Interfaces.Add(iface);

            if (name == "wlan0" && inet.Success) info.WifiIp = inet.Groups[1].Value;
        }
    }

    private static void ParseRoute(string raw, NetworkInfo info)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var g = Regex.Match(raw, @"default\s+via\s+(\d+\.\d+\.\d+\.\d+)");
        if (g.Success) info.DefaultGateway = g.Groups[1].Value;
    }

    private static void ParseDns(string raw, NetworkInfo info)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var m1 = Regex.Match(raw, @"net\.dns1\]\s*:\s*\[([^\]]+)\]");
        var m2 = Regex.Match(raw, @"net\.dns2\]\s*:\s*\[([^\]]+)\]");
        if (m1.Success) info.Dns1 = m1.Groups[1].Value.Trim();
        if (m2.Success) info.Dns2 = m2.Groups[1].Value.Trim();
    }

    private static void ParseMobile(string raw, NetworkInfo info)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var op = Regex.Match(raw, @"gsm\.sim\.operator\.alpha\]\s*:\s*\[([^\]]+)\]");
        if (op.Success) info.MobileOperator = op.Groups[1].Value.Trim();
        var nt = Regex.Match(raw, @"gsm\.network\.type\]\s*:\s*\[([^\]]+)\]");
        if (nt.Success) info.MobileNetworkType = nt.Groups[1].Value.Trim();
    }

    private static void ParseIfstat(string raw, NetworkInfo info)
    {
        if (string.IsNullOrEmpty(raw)) return;
        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            if (!line.Contains(':')) continue;
            var idx = line.IndexOf(':');
            var name = line.Substring(0, idx).Trim();
            var vals = line.Substring(idx + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (vals.Length < 9) continue;
            var iface = info.Interfaces.FirstOrDefault(i => i.Name == name);
            if (iface == null) continue;
            if (long.TryParse(vals[0], out var rx)) iface.RxBytes = rx;
            if (long.TryParse(vals[8], out var tx)) iface.TxBytes = tx;
        }
    }
}
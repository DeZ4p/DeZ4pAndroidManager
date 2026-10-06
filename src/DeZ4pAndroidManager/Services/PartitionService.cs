// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;
namespace DeZ4pAndroidManager.Services;

public class PartitionService
{
    private readonly FastbootService _fb;
    public PartitionService(FastbootService fb) => _fb = fb;

    public async Task<List<PartitionInfo>> GetPartitionsAsync(string serial, CancellationToken ct = default)
    {
        var list = new List<PartitionInfo>();
        try
        {
            var raw = await _fb.ExecuteAsync(serial, "getvar all", ct);
            var combined = (raw.StandardOutput ?? "") + "\n" + (raw.StandardError ?? "");
            var sizes = new Dictionary<string, string>();
            var types = new Dictionary<string, string>();

            foreach (var line in combined.Replace("\r", "").Split('\n'))
            {
                var t = line.Trim();
                var ms = Regex.Match(t, @"partition-size:([^:]+):\s*(.+)");
                if (ms.Success) { sizes[ms.Groups[1].Value] = ms.Groups[2].Value.Trim(); continue; }
                var mt = Regex.Match(t, @"partition-type:([^:]+):\s*(.+)");
                if (mt.Success) types[mt.Groups[1].Value] = mt.Groups[2].Value.Trim();
            }

            var all = new HashSet<string>(sizes.Keys);
            all.UnionWith(types.Keys);
            foreach (var name in all.OrderBy(x => x))
                list.Add(new PartitionInfo
                {
                    Name = name,
                    Size = sizes.TryGetValue(name, out var s) ? FormatSize(s) : "—",
                    Type = types.TryGetValue(name, out var tp) ? tp : "—"
                });
        }
        catch { }
        return list;
    }

    public async Task<(bool ok, string msg)> BackupPartitionAsync(string serial, string partition, string localPath, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, $"fetch {partition} \"{localPath}\"", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "OK" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string FormatSize(string raw)
    {
        if (long.TryParse(raw, out long bytes))
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.0} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.00} GB";
        }
        return raw;
    }
}
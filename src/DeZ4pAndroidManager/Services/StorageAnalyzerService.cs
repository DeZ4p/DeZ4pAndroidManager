// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class StorageAnalyzerService
{
    private readonly AdbService _adb;
    public StorageAnalyzerService(AdbService adb) => _adb = adb;

    public string LastDiagnostic { get; private set; } = "";

    // â”€â”€â”€ Total / Free â”€â”€â”€
    public async Task<(long total, long used, long free)> GetTotalAsync(string serial, CancellationToken ct = default)
    {
        var df = await _adb.ShellAsync(serial, "df -k /data 2>&1", ct);
        var m = Regex.Match(df ?? "", @"\s+(\d+)\s+(\d+)\s+(\d+)\s+\d+%\s+/data");
        if (m.Success)
        {
            return (
                long.Parse(m.Groups[1].Value) * 1024,
                long.Parse(m.Groups[2].Value) * 1024,
                long.Parse(m.Groups[3].Value) * 1024);
        }
        return (0, 0, 0);
    }

    // â”€â”€â”€ Top-level folders in /sdcard â”€â”€â”€
    public async Task<List<StorageCategoryItem>> GetFolderSizesAsync(string serial, CancellationToken ct = default)
    {
        var list = new List<StorageCategoryItem>();
        var cmd = "du -sk /sdcard/* 2>/dev/null | head -40";
        var raw = await _adb.ShellAsync(serial, cmd, ct);
        if (string.IsNullOrWhiteSpace(raw)) return list;

        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t) || t.StartsWith("du:")) continue;
            var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!long.TryParse(parts[0], out long kb)) continue;
            var path = parts[1].TrimEnd('/');
            var name = path.Contains('/') ? path.Substring(path.LastIndexOf('/') + 1) : path;
            if (string.IsNullOrEmpty(name) || name.StartsWith(".")) continue;

            list.Add(new StorageCategoryItem
            {
                Name = name,
                Category = "Folder",
                SizeBytes = kb * 1024,
                Icon = "\uE8B7",
                ColorHex = FolderColor(name)
            });
        }
        return list.OrderByDescending(x => x.SizeBytes).ToList();
    }

    // â”€â”€â”€ Category totals (images, video, audio, docs, apks) â”€â”€â”€
    public async Task<List<StorageCategoryItem>> GetCategorySizesAsync(string serial, CancellationToken ct = default)
    {
        var cats = new (string key, string[] exts, string icon, string color, string name)[]
        {
            ("Images", new[]{"jpg","jpeg","png","gif","bmp","webp","heic","heif"}, "\uEB9F", "#EC4899", "Images"),
            ("Videos", new[]{"mp4","mkv","avi","mov","webm","3gp","m4v","ts"},    "\uE714", "#A855F7", "Videos"),
            ("Audio",  new[]{"mp3","wav","ogg","flac","m4a","aac","opus","wma"},  "\uE8D6", "#22D3EE", "Audio"),
            ("Docs",   new[]{"pdf","doc","docx","txt","md","xls","xlsx","ppt","pptx"}, "\uE8A5", "#F59E0B", "Documents"),
            ("APKs",   new[]{"apk","apks","xapk","obb"},                          "\uE7B8", "#34D399", "APKs / OBB"),
        };

        var result = new List<StorageCategoryItem>();

        foreach (var cat in cats)
        {
            long totalBytes = 0;
            int count = 0;
            var expr = string.Join(" -o ", cat.exts.Select(e => $"-iname '*.{e}'"));
            var cmd = $"find /sdcard \\( {expr} \\) -type f -exec stat -c %s {{}} \\; 2>/dev/null | awk '{{s+=$1; n++}} END {{print s\" \"n}}'";
            try
            {
                var raw = await _adb.ShellAsync(serial, cmd, ct);
                var parts = (raw ?? "").Trim().Split(' ');
                if (parts.Length >= 2)
                {
                    if (long.TryParse(parts[0], out var s)) totalBytes = s;
                    if (int.TryParse(parts[1], out var n)) count = n;
                }
            }
            catch { }

            result.Add(new StorageCategoryItem
            {
                Name = cat.name,
                Category = cat.key,
                SizeBytes = totalBytes,
                FileCount = count,
                Icon = cat.icon,
                ColorHex = cat.color
            });
        }

        return result;
    }

    // â”€â”€â”€ Largest 20 files â”€â”€â”€
    public async Task<List<LargestFile>> GetLargestFilesAsync(string serial, CancellationToken ct = default, int limit = 25)
    {
        var list = new List<LargestFile>();
        var cmd = $"find /sdcard -type f -exec stat -c '%s|%n' {{}} \\; 2>/dev/null | sort -rn | head -{limit}";
        var raw = await _adb.ShellAsync(serial, cmd, ct);
        if (string.IsNullOrWhiteSpace(raw)) return list;

        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            var idx = t.IndexOf('|');
            if (idx <= 0) continue;
            if (!long.TryParse(t.Substring(0, idx), out long size)) continue;
            var path = t.Substring(idx + 1);
            var name = path.Contains('/') ? path.Substring(path.LastIndexOf('/') + 1) : path;
            list.Add(new LargestFile { Path = path, Name = name, SizeBytes = size });
        }
        return list;
    }

    private static string FolderColor(string name) => name.ToLowerInvariant() switch
    {
        "dcim" or "pictures" or "screenshots" => "#EC4899",
        "movies" or "video" or "recordings"   => "#A855F7",
        "music" or "audio" or "podcasts"      => "#22D3EE",
        "download" or "downloads"             => "#F59E0B",
        "documents"                           => "#4F8CFF",
        "android"                             => "#34D399",
        "whatsapp" or "telegram"              => "#818CF8",
        _                                     => "#94A3B8"
    };
}
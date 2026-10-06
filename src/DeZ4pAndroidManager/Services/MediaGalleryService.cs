// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class MediaGalleryService
{
    private readonly AdbService _adb;

    public MediaGalleryService(AdbService adb) => _adb = adb;

    /// <summary>Local folder used to cache pulled files for preview / direct open.</summary>
    public static string CacheDir => AppPaths.CachePreview;

    public static void EnsureCacheDir()
    {
        try { Directory.CreateDirectory(AppPaths.CachePreview); } catch { }
    }

    public async Task<List<MediaItem>> ListAllAsync(string serial, CancellationToken ct = default)
    {
        var list = new List<MediaItem>();
        list.AddRange(await ListImagesAsync(serial, ct));
        list.AddRange(await ListVideosAsync(serial, ct));
        list.AddRange(await ListAudioAsync(serial, ct));

        return list
            .GroupBy(m => m.FilePath, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(m => m.DateAdded)
            .ToList();
    }

    public Task<List<MediaItem>> ListImagesAsync(string serial, CancellationToken ct = default)
        => ListByUriAsync(serial, "content://media/external/images/media", MediaKind.Image, ct);

    public Task<List<MediaItem>> ListVideosAsync(string serial, CancellationToken ct = default)
        => ListByUriAsync(serial, "content://media/external/video/media", MediaKind.Video, ct);

    public Task<List<MediaItem>> ListAudioAsync(string serial, CancellationToken ct = default)
        => ListByUriAsync(serial, "content://media/external/audio/media", MediaKind.Audio, ct);

    private async Task<List<MediaItem>> ListByUriAsync(
        string serial, string uri, MediaKind kind, CancellationToken ct)
    {
        var items = new List<MediaItem>();
        var projection = "_id:_data:_display_name:_size:date_added:mime_type:width:height:duration";
        var cmd = $"content query --uri {uri} --projection {projection} 2>&1";

        string raw;
        try { raw = await _adb.ShellAsync(serial, cmd, ct); }
        catch { return items; }

        if (string.IsNullOrWhiteSpace(raw)) return items;

        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            var item = ParseMediaLine(line, kind);
            if (item != null) items.Add(item);
        }
        return items;
    }

    private static MediaItem? ParseMediaLine(string line, MediaKind kind)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        line = line.Trim();
        if (!line.StartsWith("Row:", StringComparison.Ordinal)) return null;

        string? Get(string key)
        {
            var m = Regex.Match(line, $@"\b{Regex.Escape(key)}=([^,]*)");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        var id = Get("_id");
        var data = Get("_data");
        if (string.IsNullOrEmpty(data)) return null;

        var item = new MediaItem
        {
            Id = id ?? "",
            FilePath = data,
            Kind = kind,
            DisplayName = Get("_display_name") ?? Path.GetFileName(data),
            MimeType = Get("mime_type") ?? ""
        };

        if (long.TryParse(Get("_size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long size))
            item.SizeBytes = size;

        if (long.TryParse(Get("date_added"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long da) && da > 0)
            item.DateAdded = DateTimeOffset.FromUnixTimeSeconds(da).LocalDateTime;

        if (int.TryParse(Get("width"), out int w)) item.Width = w;
        if (int.TryParse(Get("height"), out int h)) item.Height = h;
        if (long.TryParse(Get("duration"), out long d)) item.DurationMs = d;

        return item;
    }

    // ═══════════════════════════════════════════════════════════
    //  Pull to cache (used for preview + direct open)
    // ═══════════════════════════════════════════════════════════
    public async Task<string?> PullToCacheAsync(string serial, MediaItem item, CancellationToken ct = default)
    {
        EnsureCacheDir();

        // Build deterministic cache file name based on remote path
        var safeName = SanitizeFileName(item.DisplayName);
        if (string.IsNullOrEmpty(safeName)) safeName = "file";
        var hash = Math.Abs(item.FilePath.GetHashCode()).ToString("X8");
        var cached = Path.Combine(CacheDir, $"{hash}_{safeName}");

        // If already cached and size matches, reuse
        if (File.Exists(cached) && item.SizeBytes > 0 && new FileInfo(cached).Length == item.SizeBytes)
            return cached;

        // Cleanup any stale partials
        try { if (File.Exists(cached)) File.Delete(cached); } catch { }

        var r = await _adb.ExecuteRawAsync(
            $"-s {serial} pull \"{item.FilePath}\" \"{cached}\"", 600000, ct);
        return r.ExitCode == 0 && File.Exists(cached) ? cached : null;
    }

    /// <summary>Opens a media item directly with the OS default app (no save dialog).</summary>
    public async Task<bool> OpenWithSystemAsync(string serial, MediaItem item, CancellationToken ct = default)
    {
        var local = await PullToCacheAsync(serial, item, ct);
        if (local == null) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = local,
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }

    /// <summary>Show Windows "Open with" picker for the file.</summary>
    public async Task<bool> OpenWithPickerAsync(string serial, MediaItem item, CancellationToken ct = default)
    {
        var local = await PullToCacheAsync(serial, item, ct);
        if (local == null) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL \"{local}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteAsync(string serial, string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        await _adb.ShellAsync(serial, $"rm -f \"{filePath}\" 2>&1", ct);
        var check = await _adb.ShellAsync(serial, $"ls -d \"{filePath}\" 2>&1", ct);
        return check.Contains("No such") || !check.Contains(filePath);
    }

    public async Task<bool> PullAsync(string serial, string remotePath, string localPath, CancellationToken ct = default)
    {
        var r = await _adb.ExecuteRawAsync(
            $"-s {serial} pull \"{remotePath}\" \"{localPath}\"", 600000, ct);
        return r.ExitCode == 0;
    }

    // ─── Helpers ───
    public static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "file";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    public static bool IsPreviewableExtension(string path)
    {
        var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp"
                or ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".3gp"
                or ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac"
                or ".pdf" or ".txt" or ".md" or ".log" or ".json" or ".xml" => true,
            _ => false
        };
    }

    public static bool IsImageExtension(string path)
    {
        var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }
}
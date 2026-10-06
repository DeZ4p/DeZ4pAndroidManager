// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class FileManagerService
{
    private readonly AdbService _adb;

    public FileManagerService(AdbService adb) => _adb = adb;

    public async Task<string> GetHomePathAsync(string serial, CancellationToken ct = default)
    {
        var candidates = new[]
        {
            "/storage/emulated/0",
            "/sdcard",
            "/mnt/sdcard",
            "/storage/self/primary"
        };

        foreach (var path in candidates)
        {
            var r = await _adb.ShellAsync(serial, $"ls -d {path} 2>&1", ct);
            var s = r?.Trim() ?? "";
            if (!string.IsNullOrEmpty(s) && !s.Contains("No such") && !s.Contains("Permission denied"))
                return path;
        }
        return "/";
    }

    public async Task<List<FileItem>> ListDirectoryAsync(string serial, string path, CancellationToken ct = default)
    {
        var items = new List<FileItem>();
        if (string.IsNullOrWhiteSpace(path)) path = "/";
        if (!path.StartsWith("/")) path = "/" + path;

        // Try ISO time format first, then plain ls.
        // Toybox/GNU both support --time-style=long-iso on modern Android,
        // but we still fall back for old or unusual shells.
        var cmds = new[]
        {
            $"ls -la --time-style=long-iso \"{path}\" 2>/dev/null",
            $"ls -la \"{path}\" 2>/dev/null",
            $"ls -la \"{path}\" 2>&1"
        };

        string raw = "";
        foreach (var cmd in cmds)
        {
            raw = await _adb.ShellAsync(serial, cmd, ct);
            if (!string.IsNullOrWhiteSpace(raw) && !raw.Contains("No such file or directory"))
                break;
        }

        if (string.IsNullOrWhiteSpace(raw)) return items;

        foreach (var line in raw.Replace("\r", "").Split('\n'))
        {
            var item = ParseLsLine(line, path);
            if (item == null) continue;
            items.Add(item);
        }

        return items
            .OrderByDescending(i => i.IsDirectory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Multi-format ls parser. Handles:
    ///   ISO: -rw-r--r-- 1 root root 1234 2024-01-15 12:30 file.mp3
    ///   BSD short: -rw-r--r-- 1 root root 1234 Jan 15 12:30 file.mp3
    ///   BSD long:  -rw-r--r-- 1 root root 1234 Jan 15 2023 file.mp3
    /// Also handles symlinks, special chars, spaces in names.
    /// </summary>
    private static FileItem? ParseLsLine(string line, string basePath)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var trimmed = line.TrimEnd();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (trimmed.StartsWith("total ")) return null;
        if (trimmed.StartsWith("ls:")) return null;

        // perm links owner group size rest
        var match = Regex.Match(trimmed,
            @"^([dlcbps\-][rwxstST\-]{9}[\.\+@]?)\s+(\d+)\s+(\S+)\s+(\S+)\s+(\d+)\s+(.+)$");
        if (!match.Success) return null;

        var perm = match.Groups[1].Value;
        var sizeStr = match.Groups[5].Value;
        var rest = match.Groups[6].Value.Trim();

        if (!long.TryParse(sizeStr, out long size)) return null;

        DateTime modified = DateTime.MinValue;
        string name;

        // 1. ISO: 2024-01-15 12:30 NAME
        var isoM = Regex.Match(rest, @"^(\d{4})-(\d{2})-(\d{2})\s+(\d{1,2}:\d{2})(?::\d{2})?\s+(.+)$");
        if (isoM.Success)
        {
            DateTime.TryParseExact(
                $"{isoM.Groups[1].Value}-{isoM.Groups[2].Value}-{isoM.Groups[3].Value} {isoM.Groups[4].Value}",
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);
            name = isoM.Groups[5].Value;
        }
        else
        {
            // 2. BSD short: Mon DD HH:MM NAME
            var bsdM = Regex.Match(rest, @"^([A-Za-z]{3})\s+(\d{1,2})\s+(\d{1,2}:\d{2})(?::\d{2})?\s+(.+)$");
            if (bsdM.Success)
            {
                var yr = DateTime.Now.Year;
                DateTime.TryParseExact(
                    $"{bsdM.Groups[1].Value} {bsdM.Groups[2].Value} {yr} {bsdM.Groups[3].Value}",
                    "MMM d yyyy HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);
                name = bsdM.Groups[4].Value;
            }
            else
            {
                // 3. BSD long: Mon DD YYYY NAME
                var bsd2M = Regex.Match(rest, @"^([A-Za-z]{3})\s+(\d{1,2})\s+(\d{4})\s+(.+)$");
                if (bsd2M.Success)
                {
                    DateTime.TryParseExact(
                        $"{bsd2M.Groups[1].Value} {bsd2M.Groups[2].Value} {bsd2M.Groups[3].Value}",
                        "MMM d yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);
                    name = bsd2M.Groups[4].Value;
                }
                else
                {
                    // 4. Absolute fallback: split by whitespace, name = last token
                    var parts = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) return null;
                    name = parts[^1];
                }
            }
        }

        // Symlink: name -> target
        if (perm.Length > 0 && perm[0] == 'l')
        {
            var arrowIdx = name.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIdx > 0) name = name.Substring(0, arrowIdx);
        }

        name = name.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        if (name == "." || name == "..") return null;

        var trimmedBase = basePath.TrimEnd('/');
        var fullPath = string.IsNullOrEmpty(trimmedBase) ? $"/{name}" : $"{trimmedBase}/{name}";

        return new FileItem
        {
            Name = name,
            FullPath = fullPath,
            SizeBytes = size,
            Modified = modified,
            IsDirectory = perm[0] == 'd',
            IsSymlink = perm[0] == 'l',
            Permissions = perm
        };
    }

    public static string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return "/";
        var trimmed = path.TrimEnd('/');
        if (string.IsNullOrEmpty(trimmed)) return "/";
        var idx = trimmed.LastIndexOf('/');
        if (idx <= 0) return "/";
        return trimmed.Substring(0, idx);
    }

    public async Task<bool> PullAsync(string serial, string remotePath, string localPath, CancellationToken ct = default)
    {
        var r = await _adb.PullAsync(serial, remotePath, localPath, ct);
        return r.ExitCode == 0;
    }

    public async Task<bool> PushAsync(string serial, string localPath, string remotePath, CancellationToken ct = default)
    {
        var r = await _adb.ExecuteRawAsync($"-s {serial} push \"{localPath}\" \"{remotePath}\"", 120000, ct);
        return r.ExitCode == 0;
    }

    public async Task<bool> DeleteAsync(string serial, string path, CancellationToken ct = default)
    {
        await _adb.ShellAsync(serial, $"rm -rf \"{path}\"", ct);
        var check = await _adb.ShellAsync(serial, $"ls -d \"{path}\" 2>&1", ct);
        return check.Contains("No such") || check.Contains("Permission denied") || !check.Contains(path);
    }

    public async Task<bool> CreateFolderAsync(string serial, string path, CancellationToken ct = default)
    {
        await _adb.ShellAsync(serial, $"mkdir -p \"{path}\" 2>&1", ct);
        var check = await _adb.ShellAsync(serial, $"ls -d \"{path}\" 2>&1", ct);
        return check.Contains(path);
    }

    public async Task<bool> RenameAsync(string serial, string oldPath, string newPath, CancellationToken ct = default)
    {
        await _adb.ShellAsync(serial, $"mv \"{oldPath}\" \"{newPath}\" 2>&1", ct);
        var check = await _adb.ShellAsync(serial, $"ls -d \"{newPath}\" 2>&1", ct);
        return check.Contains(newPath);
    }

    public async Task<bool> CopyAsync(string serial, string sourcePath, string destPath, CancellationToken ct = default)
    {
        await _adb.ShellAsync(serial, $"cp -r \"{sourcePath}\" \"{destPath}\" 2>&1", ct);
        var check = await _adb.ShellAsync(serial, $"ls -d \"{destPath}\" 2>&1", ct);
        return check.Contains(destPath);
    }

    public async Task<bool> MoveAsync(string serial, string sourcePath, string destPath, CancellationToken ct = default)
    {
        await _adb.ShellAsync(serial, $"mv \"{sourcePath}\" \"{destPath}\" 2>&1", ct);
        var check = await _adb.ShellAsync(serial, $"ls -d \"{destPath}\" 2>&1", ct);
        return check.Contains(destPath);
    }

    public async Task<bool> PathExistsAsync(string serial, string path, CancellationToken ct = default)
    {
        var check = await _adb.ShellAsync(serial, $"ls -d \"{path}\" 2>&1", ct);
        return !check.Contains("No such") && !check.Contains("Permission denied") && check.Contains(path);
    }
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Extracts installed APKs from device to local folder.
/// Handles split APKs, obb folders, and .apks bundle creation.
/// Works Android 8 (API 26) → Android 16 (API 36).
/// </summary>
public class ApkBackupService
{
    private readonly AdbService _adb;

    public ApkBackupService(AdbService adb) => _adb = adb;

    /// <summary>Returns remote file paths of all APK splits for a package.</summary>
    public async Task<List<string>> GetApkPathsAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var paths = new List<string>();
        var r = await _adb.ShellAsync(serial, $"pm path \"{pkg}\" 2>&1", ct);
        foreach (var line in (r ?? "").Replace("\r", "").Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("package:", StringComparison.Ordinal))
            {
                var p = t.Substring(8).Trim();
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }
        }
        return paths;
    }

    /// <summary>Returns total size in bytes of all APK splits.</summary>
    public async Task<long> GetApkSizeAsync(string serial, List<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0) return 0;
        long total = 0;
        foreach (var p in paths)
        {
            var r = await _adb.ShellAsync(serial, $"ls -la \"{p}\" 2>/dev/null", ct);
            var m = Regex.Match(r ?? "", @"\s(\d+)\s+");
            if (m.Success && long.TryParse(m.Groups[1].Value, out long sz)) total += sz;
        }
        return total;
    }

    /// <summary>Pulls all splits of a package to a folder and returns the number of files pulled.</summary>
    public async Task<(int pulled, long bytes, string folder)> PullApksAsync(
        string serial, string pkg, string displayName, string outputRoot,
        IProgress<(int done, int total)>? progress,
        CancellationToken ct = default)
    {
        var paths = await GetApkPathsAsync(serial, pkg, ct);
        if (paths.Count == 0) return (0, 0, "");

        var safeName = SanitizeName(displayName);
        if (string.IsNullOrEmpty(safeName)) safeName = pkg;

        // Single APK → save as "Name_vX.apk"
        if (paths.Count == 1)
        {
            var fileName = $"{safeName}.apk";
            var dest = Path.Combine(outputRoot, fileName);
            var r = await _adb.ExecuteRawAsync($"-s {serial} pull \"{paths[0]}\" \"{dest}\"", 600000, ct);
            if (r.ExitCode == 0 && File.Exists(dest))
            {
                progress?.Report((1, 1));
                return (1, new FileInfo(dest).Length, dest);
            }
            return (0, 0, "");
        }

        // Split APKs → save to subfolder
        var folder = Path.Combine(outputRoot, safeName);
        Directory.CreateDirectory(folder);

        int done = 0;
        long bytes = 0;
        foreach (var p in paths)
        {
            ct.ThrowIfCancellationRequested();
            var name = p.Contains('/') ? p.Substring(p.LastIndexOf('/') + 1) : p;
            var dest = Path.Combine(folder, name);
            var r = await _adb.ExecuteRawAsync($"-s {serial} pull \"{p}\" \"{dest}\"", 600000, ct);
            if (r.ExitCode == 0 && File.Exists(dest))
            {
                done++;
                bytes += new FileInfo(dest).Length;
                progress?.Report((done, paths.Count));
            }
        }
        return (done, bytes, folder);
    }

    /// <summary>Packs all splits into a single .apks (ZIP) file.</summary>
    public async Task<(bool ok, string file)> PullAsApksBundleAsync(
        string serial, string pkg, string displayName, string outputRoot,
        IProgress<(int done, int total)>? progress,
        CancellationToken ct = default)
    {
        var paths = await GetApkPathsAsync(serial, pkg, ct);
        if (paths.Count == 0) return (false, "");

        // Pull each split to a temp folder
        var tempDir = Path.Combine(Path.GetTempPath(), "DeZ4p_Apks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            int done = 0;
            foreach (var p in paths)
            {
                ct.ThrowIfCancellationRequested();
                var name = p.Contains('/') ? p.Substring(p.LastIndexOf('/') + 1) : p;
                var dest = Path.Combine(tempDir, name);
                var r = await _adb.ExecuteRawAsync($"-s {serial} pull \"{p}\" \"{dest}\"", 600000, ct);
                if (r.ExitCode == 0 && File.Exists(dest))
                {
                    done++;
                    progress?.Report((done, paths.Count));
                }
            }

            var safeName = SanitizeName(displayName);
            if (string.IsNullOrEmpty(safeName)) safeName = pkg;
            var apksPath = Path.Combine(outputRoot, $"{safeName}.apks");

            if (File.Exists(apksPath)) File.Delete(apksPath);
            ZipFile.CreateFromDirectory(tempDir, apksPath, CompressionLevel.Optimal, false);

            return (true, apksPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>Pulls OBB folder if it exists (Android/obb/&lt;pkg&gt;).</summary>
    public async Task<(int count, long bytes, string folder)> PullObbAsync(
        string serial, string pkg, string outputRoot, CancellationToken ct = default)
    {
        var remoteObb = $"/sdcard/Android/obb/{pkg}";
        var check = await _adb.ShellAsync(serial, $"ls -d \"{remoteObb}\" 2>&1", ct);
        if (!check.Contains(remoteObb) || check.Contains("No such")) return (0, 0, "");

        var safeName = SanitizeName(pkg);
        var localFolder = Path.Combine(outputRoot, $"{safeName}_obb");
        Directory.CreateDirectory(localFolder);

        var r = await _adb.ExecuteRawAsync($"-s {serial} pull \"{remoteObb}\" \"{localFolder}\"", 600000, ct);
        if (r.ExitCode != 0) return (0, 0, "");

        int count = 0;
        long bytes = 0;
        try
        {
            foreach (var f in Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories))
            {
                count++;
                bytes += new FileInfo(f).Length;
            }
        }
        catch { }

        return (count, bytes, localFolder);
    }

    /// <summary>Copies all splits to device-side folder (advanced).</summary>
    public async Task<bool> CopyApksToDeviceFolderAsync(
        string serial, string pkg, string destFolder, CancellationToken ct = default)
    {
        var paths = await GetApkPathsAsync(serial, pkg, ct);
        if (paths.Count == 0) return false;

        await _adb.ShellAsync(serial, $"mkdir -p \"{destFolder}/{pkg}\" 2>&1", ct);
        bool ok = true;
        foreach (var p in paths)
        {
            var name = p.Contains('/') ? p.Substring(p.LastIndexOf('/') + 1) : p;
            var dest = $"{destFolder}/{pkg}/{name}";
            var r = await _adb.ShellAsync(serial, $"cp -f \"{p}\" \"{dest}\" 2>&1", ct);
            if (r.Contains("Permission denied") || r.Contains("No such")) ok = false;
        }
        return ok;
    }

    private static string SanitizeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var s = name;
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Application (package) operations on the connected Android device via ADB.
/// Works on Android 7 (API 24) through Android 16 (API 36).
/// </summary>
public class AppsManagerService
{
    private readonly AdbService _adb;

    public AppsManagerService(AdbService adb) => _adb = adb;

    /// <summary>Lists all installed packages (user + system) with versions and status.</summary>
    public async Task<List<AppItem>> ListAppsAsync(string serial, CancellationToken ct = default)
    {
        var apps = new List<AppItem>();

        // 1. Get package lists in one shot
        var raw = await _adb.ShellAsync(serial,
            "echo '=USER='; pm list packages -3; " +
            "echo '=SYSTEM='; pm list packages -s; " +
            "echo '=DISABLED='; pm list packages -d",
            ct);

        var userPkgs = new HashSet<string>(StringComparer.Ordinal);
        var systemPkgs = new HashSet<string>(StringComparer.Ordinal);
        var disabledPkgs = new HashSet<string>(StringComparer.Ordinal);

        var section = "";
        foreach (var line in (raw ?? "").Replace("\r", "").Split('\n'))
        {
            var t = line.Trim();
            if (t == "=USER=")     { section = "user";     continue; }
            if (t == "=SYSTEM=")   { section = "system";   continue; }
            if (t == "=DISABLED=") { section = "disabled"; continue; }
            if (!t.StartsWith("package:", StringComparison.Ordinal)) continue;
            var pkg = t.Substring(8).Trim();
            if (string.IsNullOrEmpty(pkg)) continue;

            if (section == "user") userPkgs.Add(pkg);
            else if (section == "system") systemPkgs.Add(pkg);
            else if (section == "disabled") disabledPkgs.Add(pkg);
        }

        // 2. Fetch versions in one dumpsys call
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        if (userPkgs.Count + systemPkgs.Count > 0)
        {
            var dump = await _adb.ShellAsync(serial,
                "dumpsys package | grep -E '^  Package \\[|versionName='",
                ct);

            var currentPkg = "";
            foreach (var line in (dump ?? "").Replace("\r", "").Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("Package [", StringComparison.Ordinal))
                {
                    var s = t.IndexOf('[') + 1;
                    var e = t.IndexOf(']');
                    if (e > s) currentPkg = t.Substring(s, e - s);
                }
                else if (t.StartsWith("versionName=", StringComparison.Ordinal) &&
                         !string.IsNullOrEmpty(currentPkg))
                {
                    versions[currentPkg] = t.Substring("versionName=".Length).Trim();
                    currentPkg = "";
                }
            }
        }

        // 3. Build AppItem list
        foreach (var pkg in userPkgs.Concat(systemPkgs).Distinct(StringComparer.Ordinal))
        {
            apps.Add(new AppItem
            {
                PackageName = pkg,
                DisplayName = ShortName(pkg),
                VersionName = versions.TryGetValue(pkg, out var v) ? v : "",
                IsSystem = systemPkgs.Contains(pkg),
                IsEnabled = !disabledPkgs.Contains(pkg)
            });
        }

        return apps
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ─── Actions ───

    public async Task<bool> UninstallAsync(string serial, string pkg, bool keepData, CancellationToken ct = default)
    {
        var cmd = keepData
            ? $"pm uninstall -k --user 0 \"{pkg}\""
            : $"pm uninstall --user 0 \"{pkg}\"";
        var r = await _adb.ShellAsync(serial, cmd, ct);
        return r.Contains("Success", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> EnableAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var r = await _adb.ShellAsync(serial, $"pm enable \"{pkg}\"", ct);
        return !r.Contains("Error", StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Exception", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> DisableAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var r = await _adb.ShellAsync(serial, $"pm disable-user --user 0 \"{pkg}\"", ct);
        return !r.Contains("Error", StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Exception", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> ForceStopAsync(string serial, string pkg, CancellationToken ct = default)
    {
        await _adb.ShellAsync(serial, $"am force-stop \"{pkg}\"", ct);
        return true;
    }

    public async Task<bool> ClearDataAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var r = await _adb.ShellAsync(serial, $"pm clear \"{pkg}\"", ct);
        return r.Contains("Success", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> LaunchAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var r = await _adb.ShellAsync(serial,
            $"monkey -p \"{pkg}\" -c android.intent.category.LAUNCHER 1 2>&1",
            ct);
        return !r.Contains("No activities found", StringComparison.OrdinalIgnoreCase)
            && !r.Contains("aborted", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<string>> GetApkPathsAsync(string serial, string pkg, CancellationToken ct = default)
    {
        var paths = new List<string>();
        var r = await _adb.ShellAsync(serial, $"pm path \"{pkg}\"", ct);
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

    public async Task<bool> ExportApkAsync(string serial, string pkg, string destPath,
                                            bool toFolder, CancellationToken ct = default)
    {
        var paths = await GetApkPathsAsync(serial, pkg, ct);
        if (paths.Count == 0) return false;

        if (!toFolder)
        {
            var pull = await _adb.PullAsync(serial, paths[0], destPath, ct);
            return pull.ExitCode == 0;
        }

        if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
        bool allOk = true;
        foreach (var p in paths)
        {
            var name = p.Contains('/') ? p.Substring(p.LastIndexOf('/') + 1) : p;
            var pull = await _adb.PullAsync(serial, p, Path.Combine(destPath, name), ct);
            if (pull.ExitCode != 0) allOk = false;
        }
        return allOk;
    }

    // ─── Helpers ───
    private static string ShortName(string pkg)
    {
        var parts = pkg.Split('.');
        if (parts.Length >= 2)
        {
            var last = parts[^1];
            var secondLast = parts[^2];
            if (last.Length <= 3 && secondLast.Length > 0 && secondLast.Length <= 6)
                return $"{secondLast}.{last}";
            return last;
        }
        return pkg;
    }
}
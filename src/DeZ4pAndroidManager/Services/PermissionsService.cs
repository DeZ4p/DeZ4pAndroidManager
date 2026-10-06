// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class PermissionsService
{
    private readonly AdbService _adb;
    public PermissionsService(AdbService adb) => _adb = adb;

    private static readonly string[] DangerousPerms = new[]
    {
        "CAMERA", "RECORD_AUDIO", "ACCESS_FINE_LOCATION", "ACCESS_COARSE_LOCATION",
        "READ_CONTACTS", "WRITE_CONTACTS", "READ_SMS", "SEND_SMS", "RECEIVE_SMS",
        "READ_CALL_LOG", "WRITE_CALL_LOG", "CALL_PHONE", "READ_PHONE_STATE",
        "READ_EXTERNAL_STORAGE", "WRITE_EXTERNAL_STORAGE", "BODY_SENSORS",
        "READ_CALENDAR", "WRITE_CALENDAR", "GET_ACCOUNTS", "READ_PHONE_NUMBERS"
    };

    public async Task<List<PermissionApp>> GetAppsWithPermissionsAsync(string serial, CancellationToken ct = default)
    {
        var list = new List<PermissionApp>();
        var pkgRaw = await _adb.ShellAsync(serial, "pm list packages -3 2>/dev/null", ct);
        if (string.IsNullOrWhiteSpace(pkgRaw)) return list;

        var packages = pkgRaw.Replace("\r", "").Split('\n')
            .Where(l => l.StartsWith("package:"))
            .Select(l => l.Substring(8).Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Take(50)
            .ToList();

        foreach (var pkg in packages)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var raw = await _adb.ShellAsync(serial, $"dumpsys package {pkg} 2>/dev/null | grep -E 'granted=|requested permissions' | head -60", ct);
                var app = new PermissionApp { PackageName = pkg, AppName = ShortName(pkg) };
                foreach (var line in (raw ?? "").Replace("\r", "").Split('\n'))
                {
                    var m = Regex.Match(line.Trim(), @"^([a-z_]+)\s*$|^\s*([a-zA-Z\._]+):?\s*granted=(true|false)");
                    var perm = Regex.Match(line, @"([A-Z_]{3,})");
                    if (!perm.Success) continue;
                    var permName = perm.Groups[1].Value;
                    if (!DangerousPerms.Any(d => permName.EndsWith(d))) continue;
                    var isGranted = line.Contains("granted=true");
                    if (isGranted) app.GrantedPermissions.Add(permName);
                    else app.DeniedPermissions.Add(permName);
                }
                app.DangerousCount = app.GrantedPermissions.Count;
                if (app.GrantedPermissions.Count > 0 || app.DeniedPermissions.Count > 0)
                    list.Add(app);
            }
            catch { }
        }
        return list.OrderByDescending(a => a.DangerousCount).ToList();
    }

    public async Task<(bool ok, string msg)> RevokeAsync(string serial, string pkg, string perm, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell pm revoke {pkg} android.permission.{perm}", 8000, ct);
            return (r.ExitCode == 0, string.IsNullOrEmpty(r.StandardError) ? "OK" : r.StandardError);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> GrantAsync(string serial, string pkg, string perm, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} shell pm grant {pkg} android.permission.{perm}", 8000, ct);
            return (r.ExitCode == 0, string.IsNullOrEmpty(r.StandardError) ? "OK" : r.StandardError);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string ShortName(string pkg)
    {
        var parts = pkg.Split('.');
        return parts.Length >= 2 ? parts[parts.Length - 1] : pkg;
    }
}
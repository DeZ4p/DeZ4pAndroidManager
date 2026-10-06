// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// High-level backup & restore orchestrator (no root).
/// Handles: Contacts, SMS, CallLogs, Media, AppsList, WhatsApp.
/// </summary>
public class BackupRestoreService
{
    private readonly AdbService _adb;

    public BackupRestoreService(AdbService adb) => _adb = adb;

    // ═══════════════════════════════════════════════════════════
    //  BACKUP
    // ═══════════════════════════════════════════════════════════
    public async Task<BackupManifest> CreateBackupAsync(
        string serial, string backupFolder,
        IReadOnlyList<BackupCategoryKind> categories,
        IProgress<(BackupCategoryKind kind, double percent, string note)>? progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(backupFolder);

        // Fetch basic device info
        var model = (await _adb.ShellAsync(serial, "getprop ro.product.model", ct))?.Trim() ?? "";
        var android = (await _adb.ShellAsync(serial, "getprop ro.build.version.release", ct))?.Trim() ?? "";

        var manifest = new BackupManifest
        {
            CreatedAt = DateTime.Now.ToString("o"),
            DeviceName = string.IsNullOrEmpty(model) ? "Unknown" : model,
            DeviceSerial = serial,
            AndroidVersion = string.IsNullOrEmpty(android) ? "?" : android,
            BackupName = $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm}"
        };

        foreach (var kind in categories)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                switch (kind)
                {
                    case BackupCategoryKind.Contacts:
                        manifest.Categories["contacts"] =
                            await BackupContactsAsync(serial, backupFolder, progress, ct);
                        break;
                    case BackupCategoryKind.Sms:
                        manifest.Categories["sms"] =
                            await BackupSmsAsync(serial, backupFolder, progress, ct);
                        break;
                    case BackupCategoryKind.CallLogs:
                        manifest.Categories["callLogs"] =
                            await BackupCallLogsAsync(serial, backupFolder, progress, ct);
                        break;
                    case BackupCategoryKind.Media:
                        manifest.Categories["media"] =
                            await BackupMediaAsync(serial, backupFolder, progress, ct);
                        break;
                    case BackupCategoryKind.AppsList:
                        manifest.Categories["apps"] =
                            await BackupAppsListAsync(serial, backupFolder, progress, ct);
                        break;
                    case BackupCategoryKind.WhatsApp:
                        manifest.Categories["whatsapp"] =
                            await BackupWhatsAppAsync(serial, backupFolder, progress, ct);
                        break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Continue on per-category errors
            }
        }

        manifest.ComputeTotals();
        manifest.Save(backupFolder);
        return manifest;
    }

    // ─── Contacts (vCard format) ───
    private async Task<CategoryInfo> BackupContactsAsync(string serial, string folder,
        IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        progress?.Report((BackupCategoryKind.Contacts, 10, "Reading contacts..."));

        var phonesRaw = await _adb.ShellAsync(serial,
            "content query --uri content://com.android.contacts/data/phones " +
            "--projection contact_id:display_name:data1 2>/dev/null", ct);

        var emailsRaw = await _adb.ShellAsync(serial,
            "content query --uri content://com.android.contacts/data/emails " +
            "--projection contact_id:display_name:data1 2>/dev/null", ct);

        var map = new Dictionary<string, (string Name, List<string> Phones, List<string> Emails)>();

        void ParseRows(string raw, bool isEmail)
        {
            foreach (var line in (raw ?? "").Replace("\r", "").Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("Row:", StringComparison.Ordinal)) continue;

                var idM = Regex.Match(t, @"contact_id=(\d+)");
                var nmM = Regex.Match(t, @"display_name=([^,]*)");
                var dataM = Regex.Match(t, @"data1=([^,]*)");
                if (!idM.Success || !dataM.Success) continue;

                var id = idM.Groups[1].Value;
                var name = nmM.Success ? nmM.Groups[1].Value.Trim() : "Unknown";
                var value = dataM.Groups[1].Value.Trim();

                if (!map.TryGetValue(id, out var e))
                {
                    e = (name, new List<string>(), new List<string>());
                }
                if (isEmail) e.Emails.Add(value);
                else e.Phones.Add(value);
                map[id] = e;
            }
        }

        ParseRows(phonesRaw, false);
        ParseRows(emailsRaw, true);

        progress?.Report((BackupCategoryKind.Contacts, 50, $"Building vCard ({map.Count} contacts)..."));

        // Write vCard file
        var sb = new StringBuilder();
        foreach (var kv in map)
        {
            var e = kv.Value;
            sb.AppendLine("BEGIN:VCARD");
            sb.AppendLine("VERSION:3.0");
            sb.AppendLine($"FN:{EscapeVcf(e.Name)}");
            sb.AppendLine($"N:{EscapeVcf(e.Name)};;;;");
            foreach (var ph in e.Phones)
                sb.AppendLine($"TEL;TYPE=CELL:{EscapeVcf(ph)}");
            foreach (var em in e.Emails)
                sb.AppendLine($"EMAIL:{EscapeVcf(em)}");
            sb.AppendLine("END:VCARD");
        }

        var file = Path.Combine(folder, "contacts.vcf");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);

        progress?.Report((BackupCategoryKind.Contacts, 100, $"{map.Count} contacts"));

        return new CategoryInfo
        {
            Count = map.Count,
            Bytes = new FileInfo(file).Length,
            File = "contacts.vcf"
        };
    }

    private static string EscapeVcf(string s)
        => (s ?? "").Replace("\n", "\\n").Replace(",", "\\,").Replace(";", "\\;");

    // ─── SMS (JSON) ───
    private async Task<CategoryInfo> BackupSmsAsync(string serial, string folder,
        IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        progress?.Report((BackupCategoryKind.Sms, 10, "Reading SMS..."));

        var raw = await _adb.ShellAsync(serial,
            "content query --uri content://sms --projection _id:address:body:date:type:read 2>/dev/null",
            ct);

        var sb = new StringBuilder();
        sb.AppendLine("[");
        int count = 0;
        bool first = true;

        foreach (var line in (raw ?? "").Replace("\r", "").Split('\n'))
        {
            ct.ThrowIfCancellationRequested();
            var t = line.Trim();
            if (!t.StartsWith("Row:", StringComparison.Ordinal)) continue;

            var idM = Regex.Match(t, @"_id=(\d+)");
            var addrM = Regex.Match(t, @"address=([^,]*)");
            var bodyM = Regex.Match(t, @"body=(.*?)(?:,\s*date=\d+|$)", RegexOptions.Singleline);
            var dateM = Regex.Match(t, @"date=(\d+)");
            var typeM = Regex.Match(t, @"type=(\d+)");
            var readM = Regex.Match(t, @"read=(\d+)");

            if (!idM.Success) continue;

            var obj = new
            {
                id = idM.Groups[1].Value,
                address = addrM.Success ? addrM.Groups[1].Value.Trim() : "",
                body = bodyM.Success ? bodyM.Groups[1].Value.TrimEnd(',', ' ') : "",
                date = dateM.Success ? dateM.Groups[1].Value : "0",
                type = typeM.Success ? typeM.Groups[1].Value : "1",
                read = readM.Success ? readM.Groups[1].Value : "1"
            };

            if (!first) sb.AppendLine(",");
            first = false;
            sb.Append("  {");
            sb.Append($"\"id\":\"{EscapeJson(obj.id)}\",");
            sb.Append($"\"address\":\"{EscapeJson(obj.address)}\",");
            sb.Append($"\"body\":\"{EscapeJson(obj.body)}\",");
            sb.Append($"\"date\":{obj.date},");
            sb.Append($"\"type\":{obj.type},");
            sb.Append($"\"read\":{obj.read}");
            sb.Append("}");
            count++;

            if (count % 200 == 0)
                progress?.Report((BackupCategoryKind.Sms, Math.Min(90, count / 20.0), $"{count} messages..."));
        }

        sb.AppendLine();
        sb.AppendLine("]");

        var file = Path.Combine(folder, "sms.json");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);

        progress?.Report((BackupCategoryKind.Sms, 100, $"{count} messages"));

        return new CategoryInfo
        {
            Count = count,
            Bytes = new FileInfo(file).Length,
            File = "sms.json"
        };
    }

    // ─── Call Logs (JSON) ───
    private async Task<CategoryInfo> BackupCallLogsAsync(string serial, string folder,
        IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        progress?.Report((BackupCategoryKind.CallLogs, 10, "Reading call logs..."));

        var raw = await _adb.ShellAsync(serial,
            "content query --uri content://call_log/calls " +
            "--projection _id:number:name:date:duration:type:new 2>/dev/null", ct);

        var sb = new StringBuilder();
        sb.AppendLine("[");
        int count = 0;
        bool first = true;

        foreach (var line in (raw ?? "").Replace("\r", "").Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith("Row:", StringComparison.Ordinal)) continue;

            string Get(string key)
            {
                var m = Regex.Match(t, $@"\b{key}=([^,]*)");
                return m.Success ? m.Groups[1].Value.Trim() : "";
            }

            var obj = new
            {
                id = Get("_id"),
                number = Get("number"),
                name = Get("name"),
                date = Get("date"),
                duration = Get("duration"),
                type = Get("type"),
                @new = Get("new")
            };

            if (string.IsNullOrEmpty(obj.id)) continue;

            if (!first) sb.AppendLine(",");
            first = false;
            sb.Append("  {");
            sb.Append($"\"id\":\"{EscapeJson(obj.id)}\",");
            sb.Append($"\"number\":\"{EscapeJson(obj.number)}\",");
            sb.Append($"\"name\":\"{EscapeJson(obj.name)}\",");
            sb.Append($"\"date\":\"{EscapeJson(obj.date)}\",");
            sb.Append($"\"duration\":\"{EscapeJson(obj.duration)}\",");
            sb.Append($"\"type\":\"{EscapeJson(obj.type)}\"");
            sb.Append("}");
            count++;
        }

        sb.AppendLine();
        sb.AppendLine("]");

        var file = Path.Combine(folder, "call_logs.json");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);

        progress?.Report((BackupCategoryKind.CallLogs, 100, $"{count} entries"));

        return new CategoryInfo
        {
            Count = count,
            Bytes = new FileInfo(file).Length,
            File = "call_logs.json"
        };
    }

    // ─── Media (folder pull) ───
    private async Task<CategoryInfo> BackupMediaAsync(string serial, string folder,
        IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        var mediaFolder = Path.Combine(folder, "media");
        Directory.CreateDirectory(mediaFolder);

        var subfolders = new[]
        {
            "DCIM", "Pictures", "Movies", "Music", "Download",
            "Documents", "Recordings", "Screenshots"
        };

        int totalCount = 0;
        long totalBytes = 0;
        int done = 0;

        foreach (var sub in subfolders)
        {
            ct.ThrowIfCancellationRequested();
            var remote = $"/sdcard/{sub}";
            var check = await _adb.ShellAsync(serial, $"ls -d \"{remote}\" 2>&1", ct);
            if (check.Contains("No such") || check.Contains("Permission denied")) { done++; continue; }

            progress?.Report((BackupCategoryKind.Media, (double)done / subfolders.Length * 100, sub));
            var local = Path.Combine(mediaFolder, sub);
            try
            {
                await _adb.ExecuteRawAsync($"-s {serial} pull \"{remote}\" \"{local}\"", 900000, ct);
            }
            catch { }

            try
            {
                if (Directory.Exists(local))
                {
                    foreach (var f in Directory.GetFiles(local, "*", SearchOption.AllDirectories))
                    {
                        totalCount++;
                        try { totalBytes += new FileInfo(f).Length; } catch { }
                    }
                }
            }
            catch { }
            done++;
        }

        progress?.Report((BackupCategoryKind.Media, 100, $"{totalCount} files"));

        return new CategoryInfo
        {
            Count = totalCount,
            Bytes = totalBytes,
            Folder = "media"
        };
    }

    // ─── Apps List (text) ───
    private async Task<CategoryInfo> BackupAppsListAsync(string serial, string folder,
        IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        progress?.Report((BackupCategoryKind.AppsList, 20, "Reading package list..."));

        var raw = await _adb.ShellAsync(serial, "pm list packages -3 2>/dev/null", ct);
        var lines = (raw ?? "").Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("package:", StringComparison.Ordinal))
            .Select(l => l.Substring(8))
            .Where(p => !string.IsNullOrEmpty(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var file = Path.Combine(folder, "apps.txt");
        File.WriteAllText(file, string.Join("\n", lines), Encoding.UTF8);

        progress?.Report((BackupCategoryKind.AppsList, 100, $"{lines.Count} apps"));

        return new CategoryInfo
        {
            Count = lines.Count,
            Bytes = new FileInfo(file).Length,
            File = "apps.txt"
        };
    }

    // ─── WhatsApp ───
    private async Task<CategoryInfo> BackupWhatsAppAsync(string serial, string folder,
        IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        progress?.Report((BackupCategoryKind.WhatsApp, 10, "Reading WhatsApp folder..."));

        var remote = "/sdcard/WhatsApp";
        var check = await _adb.ShellAsync(serial, $"ls -d \"{remote}\" 2>&1", ct);
        if (check.Contains("No such") || check.Contains("Permission denied"))
        {
            progress?.Report((BackupCategoryKind.WhatsApp, 100, "Not installed"));
            return new CategoryInfo { Count = 0, Bytes = 0 };
        }

        var local = Path.Combine(folder, "whatsapp");
        try
        {
            await _adb.ExecuteRawAsync($"-s {serial} pull \"{remote}\" \"{local}\"", 1800000, ct);
        }
        catch { }

        int count = 0;
        long bytes = 0;
        try
        {
            if (Directory.Exists(local))
            {
                foreach (var f in Directory.GetFiles(local, "*", SearchOption.AllDirectories))
                {
                    count++;
                    try { bytes += new FileInfo(f).Length; } catch { }
                }
            }
        }
        catch { }

        progress?.Report((BackupCategoryKind.WhatsApp, 100, $"{count} files"));

        return new CategoryInfo
        {
            Count = count,
            Bytes = bytes,
            Folder = "whatsapp"
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  RESTORE
    // ═══════════════════════════════════════════════════════════
    public async Task<(int ok, int fail)> RestoreAsync(
        string serial, string backupFolder, BackupManifest manifest,
        IReadOnlyList<BackupCategoryKind> categories,
        IProgress<(BackupCategoryKind kind, double percent, string note)>? progress,
        CancellationToken ct = default)
    {
        int ok = 0, fail = 0;

        foreach (var kind in categories)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                bool success = kind switch
                {
                    BackupCategoryKind.Contacts => await RestoreContactsAsync(serial, backupFolder, manifest, progress, ct),
                    BackupCategoryKind.Sms => await RestoreSmsAsync(serial, backupFolder, manifest, progress, ct),
                    BackupCategoryKind.CallLogs => await RestoreCallLogsAsync(serial, backupFolder, manifest, progress, ct),
                    BackupCategoryKind.Media => await RestoreMediaAsync(serial, backupFolder, manifest, progress, ct),
                    BackupCategoryKind.AppsList => await RestoreAppsListAsync(serial, backupFolder, manifest, progress, ct),
                    _ => false
                };
                if (success) ok++; else fail++;
            }
            catch (OperationCanceledException) { throw; }
            catch { fail++; }
        }

        return (ok, fail);
    }

    private async Task<bool> RestoreContactsAsync(string serial, string folder,
        BackupManifest manifest, IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        if (!manifest.Categories.ContainsKey("contacts")) return false;
        var file = Path.Combine(folder, "contacts.vcf");
        if (!File.Exists(file)) return false;

        progress?.Report((BackupCategoryKind.Contacts, 10, "Pushing vCard to device..."));

        // Push vCard to device; user imports via Contacts app (safest no-root way)
        var remotePath = "/sdcard/DeZ4p_restore_contacts.vcf";
        var push = await _adb.ExecuteRawAsync($"-s {serial} push \"{file}\" \"{remotePath}\"", 120000, ct);
        if (push.ExitCode != 0) return false;

        progress?.Report((BackupCategoryKind.Contacts, 80, "Opening Contacts import..."));

        // Trigger import via intent - opens device's import UI
        await _adb.ShellAsync(serial,
            $"am start -a android.intent.action.VIEW -d file://{remotePath} -t text/x-vcard 2>&1", ct);

        progress?.Report((BackupCategoryKind.Contacts, 100, "Import UI opened on device"));
        return true;
    }

    private async Task<bool> RestoreSmsAsync(string serial, string folder,
        BackupManifest manifest, IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        if (!manifest.Categories.ContainsKey("sms")) return false;
        var file = Path.Combine(folder, "sms.json");
        if (!File.Exists(file)) return false;

        var json = File.ReadAllText(file);
        var items = ParseSimpleJsonArray(json);
        if (items.Count == 0) return false;

        progress?.Report((BackupCategoryKind.Sms, 5, $"Restoring {items.Count} messages..."));

        int done = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var addr = EscapeShellForBind(item.GetValueOrDefault("address", ""));
                var body = EscapeShellForBind(item.GetValueOrDefault("body", ""));
                var date = item.GetValueOrDefault("date", "0");
                var type = item.GetValueOrDefault("type", "1");
                var read = item.GetValueOrDefault("read", "1");

                await _adb.ShellAsync(serial,
                    $"content insert --uri content://sms " +
                    $"--bind address:s:'{addr}' --bind body:s:'{body}' " +
                    $"--bind date:l:{date} --bind type:i:{type} --bind read:i:{read} 2>&1", ct);
            }
            catch { }
            done++;

            if (done % 25 == 0)
                progress?.Report((BackupCategoryKind.Sms, 5 + (double)done / items.Count * 90, $"{done}/{items.Count}"));
        }

        progress?.Report((BackupCategoryKind.Sms, 100, $"{done} messages restored"));
        return true;
    }

    private async Task<bool> RestoreCallLogsAsync(string serial, string folder,
        BackupManifest manifest, IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        if (!manifest.Categories.ContainsKey("callLogs")) return false;
        var file = Path.Combine(folder, "call_logs.json");
        if (!File.Exists(file)) return false;

        var json = File.ReadAllText(file);
        var items = ParseSimpleJsonArray(json);
        if (items.Count == 0) return false;

        progress?.Report((BackupCategoryKind.CallLogs, 5, $"Restoring {items.Count} entries..."));

        int done = 0;
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var number = EscapeShellForBind(item.GetValueOrDefault("number", ""));
                var date = item.GetValueOrDefault("date", "0");
                var duration = item.GetValueOrDefault("duration", "0");
                var type = item.GetValueOrDefault("type", "1");

                await _adb.ShellAsync(serial,
                    $"content insert --uri content://call_log/calls " +
                    $"--bind number:s:'{number}' --bind date:l:{date} " +
                    $"--bind duration:l:{duration} --bind type:i:{type} 2>&1", ct);
            }
            catch { }
            done++;

            if (done % 25 == 0)
                progress?.Report((BackupCategoryKind.CallLogs, 5 + (double)done / items.Count * 90, $"{done}/{items.Count}"));
        }

        progress?.Report((BackupCategoryKind.CallLogs, 100, $"{done} entries restored"));
        return true;
    }

    private async Task<bool> RestoreMediaAsync(string serial, string folder,
        BackupManifest manifest, IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        if (!manifest.Categories.ContainsKey("media")) return false;
        var mediaFolder = Path.Combine(folder, "media");
        if (!Directory.Exists(mediaFolder)) return false;

        var subs = Directory.GetDirectories(mediaFolder);
        int done = 0;
        foreach (var sub in subs)
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(sub);
            progress?.Report((BackupCategoryKind.Media, (double)done / subs.Length * 100, $"Pushing {name}..."));

            var remote = $"/sdcard/{name}";
            try
            {
                await _adb.ExecuteRawAsync($"-s {serial} push \"{sub}\" \"{remote}\"", 1800000, ct);
            }
            catch { }
            done++;
        }

        progress?.Report((BackupCategoryKind.Media, 100, "Media pushed"));
        return true;
    }

    private async Task<bool> RestoreAppsListAsync(string serial, string folder,
        BackupManifest manifest, IProgress<(BackupCategoryKind, double, string)>? progress, CancellationToken ct)
    {
        if (!manifest.Categories.ContainsKey("apps")) return false;
        var file = Path.Combine(folder, "apps.txt");
        if (!File.Exists(file)) return false;

        var lines = File.ReadAllLines(file)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        progress?.Report((BackupCategoryKind.AppsList, 5, $"Re-installing {lines.Count} apps..."));

        int done = 0;
        foreach (var pkg in lines)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Restores previously-installed apps on same device (no downloads needed)
                await _adb.ShellAsync(serial, $"pm install-existing --user 0 \"{pkg}\" 2>&1", ct);
            }
            catch { }
            done++;

            if (done % 5 == 0)
                progress?.Report((BackupCategoryKind.AppsList, 5 + (double)done / lines.Count * 90, $"{done}/{lines.Count}"));
        }

        progress?.Report((BackupCategoryKind.AppsList, 100, $"{done} apps re-enabled"));
        return true;
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════
    private static string EscapeJson(string s)
        => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    private static string EscapeShellForBind(string s)
        => (s ?? "").Replace("'", "'\\''");

    private static List<Dictionary<string, string>> ParseSimpleJsonArray(string json)
    {
        // Lightweight parse - the JSON we wrote is predictable & flat.
        var result = new List<Dictionary<string, string>>();
        try
        {
            int depth = 0;
            var current = new StringBuilder();
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (escape) { current.Append(c); escape = false; continue; }
                if (c == '\\') { current.Append(c); escape = true; continue; }
                if (c == '"') { inString = !inString; current.Append(c); continue; }

                if (!inString)
                {
                    if (c == '{') { depth++; current.Clear(); current.Append(c); }
                    else if (c == '}')
                    {
                        current.Append(c);
                        depth--;
                        if (depth == 0)
                        {
                            var dict = ParseFlatObject(current.ToString());
                            if (dict.Count > 0) result.Add(dict);
                            current.Clear();
                        }
                    }
                    else if (depth > 0) current.Append(c);
                }
                else if (depth > 0) current.Append(c);
            }
        }
        catch { }
        return result;
    }

    private static Dictionary<string, string> ParseFlatObject(string obj)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            // Strip braces
            var inner = obj.Trim().TrimStart('{').TrimEnd('}');
            var pairs = SplitTopLevel(inner);
            foreach (var pair in pairs)
            {
                int colon = pair.IndexOf(':');
                if (colon <= 0) continue;
                var k = pair.Substring(0, colon).Trim().Trim('"');
                var v = pair.Substring(colon + 1).Trim().Trim('"');
                v = v.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                dict[k] = v;
            }
        }
        catch { }
        return dict;
    }

    private static List<string> SplitTopLevel(string s)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        bool inStr = false, esc = false;
        foreach (char c in s)
        {
            if (esc) { cur.Append(c); esc = false; continue; }
            if (c == '\\') { cur.Append(c); esc = true; continue; }
            if (c == '"') { inStr = !inStr; cur.Append(c); continue; }
            if (c == ',' && !inStr) { parts.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }
}
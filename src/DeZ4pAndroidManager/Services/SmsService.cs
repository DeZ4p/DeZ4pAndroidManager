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

public class SmsService
{
    private readonly AdbService _adb;
    private const int ChunkSize = 400;

    public SmsService(AdbService adb) => _adb = adb;

    public string LastDiagnostic { get; private set; } = "";

    private static readonly string[] TempPaths = new[]
    {
        "/data/local/tmp/dez4p_sms.txt",
        "/data/local/tmp/_dez4p_sms.tmp",
        "/sdcard/dez4p_sms.txt"
    };

    public async Task<List<SmsMessage>> GetAllMessagesAsync(string serial, CancellationToken ct = default)
    {
        var diag = new StringBuilder();

        // ─── 1. Find writable path ───
        string? devicePath = null;
        foreach (var candidate in TempPaths)
        {
            try
            {
                var r = await _adb.ShellAsync(serial,
                    $"echo x > \"{candidate}\" 2>/dev/null && echo __OK__; rm -f \"{candidate}\"", ct);
                if (r.Contains("__OK__")) { devicePath = candidate; break; }
            }
            catch { }
        }

        if (devicePath == null)
        {
            diag.AppendLine("No writable path");
            LastDiagnostic = diag.ToString();
            return new List<SmsMessage>();
        }
        diag.AppendLine($"Path: {devicePath}");

        // ─── 2. Get list of all _id values (small payload) ───
        var idsRaw = await _adb.ShellAsync(serial,
            "content query --uri content://sms --projection _id 2>&1", ct);

        var ids = new List<long>();
        foreach (Match m in Regex.Matches(idsRaw ?? "", @"_id=(\d+)"))
        {
            if (long.TryParse(m.Groups[1].Value, out long id)) ids.Add(id);
        }
        ids = ids.Distinct().OrderBy(x => x).ToList();
        diag.AppendLine($"Device IDs: {ids.Count}");

        // ─── 3. Try single-shot first (fast for small DBs) ───
        await _adb.ShellAsync(serial, $"rm -f \"{devicePath}\"", ct);
        var singleCmd = $"content query --uri content://sms " +
                        $"--projection _id:address:body:date:type:read " +
                        $"> \"{devicePath}\" 2>&1";
        try { await _adb.ShellAsync(serial, singleCmd, ct); } catch { }

        var sizeRaw = await _adb.ShellAsync(serial, $"wc -c < \"{devicePath}\"", ct);
        long.TryParse(sizeRaw?.Trim(), out long deviceBytes);
        diag.AppendLine($"Single-shot file: {deviceBytes} bytes");

        // Parse what we got
        var localTmp = Path.Combine(Path.GetTempPath(), $"dez4p_sms_{Guid.NewGuid():N}.txt");
        var pull = await _adb.ExecuteRawAsync(
            $"-s {serial} pull \"{devicePath}\" \"{localTmp}\"", 180000, ct);
        var parsed = new List<SmsMessage>();
        if (pull.ExitCode == 0 && File.Exists(localTmp))
        {
            string raw;
            try { raw = File.ReadAllText(localTmp, Encoding.UTF8); }
            catch { raw = File.ReadAllText(localTmp); }
            parsed = ParseAllMessages(raw);
            diag.AppendLine($"Single-shot parsed: {parsed.Count}");
            try { File.Delete(localTmp); } catch { }
        }

        // ─── 4. If parsed < IDs count, do chunked loading ───
        if (ids.Count > 0 && parsed.Count < ids.Count - 2)
        {
            diag.AppendLine("→ Falling back to chunked loading");

            await _adb.ShellAsync(serial, $"rm -f \"{devicePath}\"", ct);

            long minId = ids.Min();
            long maxId = ids.Max();
            long start = minId - 1;

            while (start < maxId)
            {
                ct.ThrowIfCancellationRequested();
                long end = start + ChunkSize;

                var cmd = $"content query --uri content://sms " +
                          $"--projection _id:address:body:date:type:read " +
                          $"--where '_id > {start} AND _id <= {end}' " +
                          $">> \"{devicePath}\" 2>&1";
                try { await _adb.ShellAsync(serial, cmd, ct); } catch { }
                start = end;
            }

            var sizeRaw2 = await _adb.ShellAsync(serial, $"wc -c < \"{devicePath}\"", ct);
            long.TryParse(sizeRaw2?.Trim(), out long deviceBytes2);
            diag.AppendLine($"Chunked file: {deviceBytes2} bytes");

            var localTmp2 = Path.Combine(Path.GetTempPath(), $"dez4p_sms2_{Guid.NewGuid():N}.txt");
            var pull2 = await _adb.ExecuteRawAsync(
                $"-s {serial} pull \"{devicePath}\" \"{localTmp2}\"", 240000, ct);
            diag.AppendLine($"Chunked pull exit: {pull2.ExitCode}");

            if (pull2.ExitCode == 0 && File.Exists(localTmp2))
            {
                string raw2;
                try { raw2 = File.ReadAllText(localTmp2, Encoding.UTF8); }
                catch { raw2 = File.ReadAllText(localTmp2); }
                var chunked = ParseAllMessages(raw2);
                diag.AppendLine($"Chunked parsed: {chunked.Count}");
                try { File.Delete(localTmp2); } catch { }

                if (chunked.Count > parsed.Count) parsed = chunked;
            }
        }

        try { await _adb.ShellAsync(serial, $"rm -f \"{devicePath}\" 2>/dev/null", ct); } catch { }

        // ─── 5. Final sort + dedup ───
        var final = parsed
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(m => m.Date)
            .ThenBy(m => long.TryParse(m.Id, out long v) ? v : 0)
            .ToList();

        diag.AppendLine($"Final: {final.Count}");
        LastDiagnostic = diag.ToString();
        return final;
    }

    private static List<SmsMessage> ParseAllMessages(string raw)
    {
        var result = new List<SmsMessage>();
        if (string.IsNullOrEmpty(raw)) return result;

        raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        var chunks = Regex.Split(raw, @"(?=^Row:\s*\d+\s)", RegexOptions.Multiline);
        foreach (var chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk)) continue;
            if (!chunk.TrimStart().StartsWith("Row:", StringComparison.Ordinal)) continue;

            var msg = ParseSmsChunk(chunk);
            if (msg != null) result.Add(msg);
        }
        return result;
    }

    private static SmsMessage? ParseSmsChunk(string chunk)
    {
        var idM = Regex.Match(chunk, @"_id=(\d+)");
        if (!idM.Success) return null;

        var msg = new SmsMessage { Id = idM.Groups[1].Value };

        int tailIdx = chunk.LastIndexOf(", date=", StringComparison.Ordinal);
        if (tailIdx < 0) return null;

        var tail = chunk.Substring(tailIdx);
        var tailM = Regex.Match(tail, @",\s*date=(\d+),\s*type=(\d+),\s*read=(\d+)");
        if (!tailM.Success) return null;

        if (long.TryParse(tailM.Groups[1].Value, out long ms))
            msg.Date = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;

        msg.IsIncoming = tailM.Groups[2].Value == "1";
        msg.IsRead = tailM.Groups[3].Value == "1";

        int addrStart = chunk.IndexOf("address=", StringComparison.Ordinal);
        int bodyStart = chunk.IndexOf(", body=", StringComparison.Ordinal);

        if (addrStart >= 0 && bodyStart > addrStart)
            msg.Address = chunk.Substring(addrStart + 8, bodyStart - addrStart - 8).Trim();

        if (bodyStart >= 0 && tailIdx > bodyStart)
            msg.Body = chunk.Substring(bodyStart + 7, tailIdx - bodyStart - 7);

        return msg;
    }

    public List<SmsConversation> BuildConversations(
        List<SmsMessage> allMessages,
        Dictionary<string, string> contactNameByNumber)
    {
        var byAddress = new Dictionary<string, SmsConversation>(StringComparer.Ordinal);

        foreach (var m in allMessages)
        {
            var key = NormalizeNumber(m.Address);
            if (string.IsNullOrEmpty(key)) continue;

            if (!byAddress.TryGetValue(key, out var conv))
            {
                conv = new SmsConversation { ThreadId = key, Address = m.Address };
                byAddress[key] = conv;
            }

            conv.Messages.Add(m);
            if (m.Date > conv.LastDate) { conv.LastDate = m.Date; conv.LastMessage = m.Body; }
            conv.MessageCount++;
            if (m.IsIncoming && !m.IsRead) conv.UnreadCount++;
        }

        foreach (var conv in byAddress.Values)
        {
            if (contactNameByNumber.TryGetValue(NormalizeNumber(conv.Address), out var name)
                && !string.IsNullOrWhiteSpace(name))
                conv.ContactName = name;
        }

        return byAddress.Values.OrderByDescending(c => c.LastDate).ToList();
    }

    public async Task<List<SmsConversation>> GetConversationsAsync(
        string serial, Dictionary<string, string> map, CancellationToken ct = default)
    {
        var all = await GetAllMessagesAsync(serial, ct);
        return BuildConversations(all, map);
    }

    public List<SmsMessage> FilterByAddress(List<SmsMessage> all, string address)
    {
        var key = NormalizeNumber(address);
        if (string.IsNullOrEmpty(key)) return all;
        return all.Where(m => NormalizeNumber(m.Address) == key).OrderBy(m => m.Date).ToList();
    }

    public async Task<bool> OpenComposerAsync(string serial, string phoneNumber, string body,
                                              CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return false;
        var safeNum = phoneNumber.Replace("'", "").Replace("\"", "").Replace(" ", "");
        var safeBody = EscapeShellArg(body ?? "");
        var cmd = $"am start -a android.intent.action.SENDTO -d sms:{safeNum} --es sms_body {safeBody}";
        var r = await _adb.ShellAsync(serial, cmd, ct);
        return !r.Contains("Error", StringComparison.OrdinalIgnoreCase)
            && !r.Contains("Exception", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> OpenDialerAsync(string serial, string phoneNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return false;
        var safeNum = phoneNumber.Replace("'", "").Replace("\"", "").Replace(" ", "");
        var cmd = $"am start -a android.intent.action.DIAL -d tel:{safeNum}";
        var r = await _adb.ShellAsync(serial, cmd, ct);
        return !r.Contains("Error", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeShellArg(string s)
        => "'" + (s ?? "").Replace("'", "'\\''") + "'";

    public static string NormalizeNumber(string? n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "";
        var digits = new string(n.Where(char.IsDigit).ToArray());
        if (digits.Length > 10) digits = digits.Substring(digits.Length - 10);
        return digits;
    }
}
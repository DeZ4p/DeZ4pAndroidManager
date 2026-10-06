// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Reads contacts from the connected Android device via content provider.
/// Works on Android 7 → 16 with graceful degradation.
/// </summary>
public class ContactsService
{
    private readonly AdbService _adb;

    public ContactsService(AdbService adb) => _adb = adb;

    public async Task<List<ContactItem>> ListContactsAsync(string serial, CancellationToken ct = default)
    {
        var map = new Dictionary<string, ContactItem>(StringComparer.Ordinal);

        // Phones
        var phonesRaw = await _adb.ShellAsync(serial,
            "content query --uri content://com.android.contacts/data/phones --projection contact_id:display_name:data1 2>/dev/null",
            ct);

        foreach (var line in (phonesRaw ?? "").Replace("\r", "").Split('\n'))
        {
            var d = ParseRow(line);
            if (d == null) continue;
            var id = Get(d, "contact_id");
            if (string.IsNullOrEmpty(id)) continue;

            if (!map.TryGetValue(id, out var contact))
            {
                contact = new ContactItem
                {
                    ContactId = id,
                    DisplayName = Get(d, "display_name") ?? "Unknown"
                };
                map[id] = contact;
            }

            var phone = Get(d, "data1");
            if (!string.IsNullOrWhiteSpace(phone) && !contact.PhoneNumbers.Contains(phone))
                contact.PhoneNumbers.Add(phone);
        }

        // Emails
        var emailsRaw = await _adb.ShellAsync(serial,
            "content query --uri content://com.android.contacts/data/emails --projection contact_id:display_name:data1 2>/dev/null",
            ct);

        foreach (var line in (emailsRaw ?? "").Replace("\r", "").Split('\n'))
        {
            var d = ParseRow(line);
            if (d == null) continue;
            var id = Get(d, "contact_id");
            if (string.IsNullOrEmpty(id)) continue;

            if (!map.TryGetValue(id, out var contact))
            {
                contact = new ContactItem
                {
                    ContactId = id,
                    DisplayName = Get(d, "display_name") ?? "Unknown"
                };
                map[id] = contact;
            }

            var email = Get(d, "data1");
            if (!string.IsNullOrWhiteSpace(email) && !contact.Emails.Contains(email))
                contact.Emails.Add(email);
        }

        return map.Values
            .Where(c => c.PhoneNumbers.Count > 0 || c.Emails.Count > 0)
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ─── Parsing: "Row: N key1=val1, key2=val2, key3=val3" ───
    private static Dictionary<string, string>? ParseRow(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        line = line.Trim();
        if (!line.StartsWith("Row:", StringComparison.Ordinal)) return null;

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var parts = Regex.Split(line, @",\s*(?=[a-zA-Z_][a-zA-Z0-9_]*=)");

        foreach (var part in parts)
        {
            var p = part.Trim();

            // Strip "Row: N " prefix from the first element
            if (p.StartsWith("Row:", StringComparison.Ordinal))
            {
                int sp1 = p.IndexOf(' ');
                if (sp1 < 0) continue;
                int sp2 = p.IndexOf(' ', sp1 + 1);
                if (sp2 < 0) continue;
                p = p.Substring(sp2 + 1);
            }

            int eq = p.IndexOf('=');
            if (eq <= 0) continue;
            var key = p.Substring(0, eq).Trim();
            var val = p.Substring(eq + 1);
            if (!string.IsNullOrEmpty(key)) dict[key] = val;
        }

        return dict;
    }

    private static string? Get(Dictionary<string, string> d, string key)
        => d.TryGetValue(key, out var v) ? v : null;
}
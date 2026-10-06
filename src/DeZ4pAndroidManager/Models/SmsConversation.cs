// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

public class SmsConversation
{
    public string ThreadId { get; set; } = "";
    public string Address { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public DateTime LastDate { get; set; } = DateTime.MinValue;
    public int MessageCount { get; set; }
    public int UnreadCount { get; set; }
    public List<SmsMessage> Messages { get; set; } = new();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(ContactName) ? ContactName : Address;

    public string Subtitle => !string.IsNullOrWhiteSpace(ContactName) ? Address : "";

    public string Initial
    {
        get
        {
            var n = (DisplayName ?? "").Trim();
            if (n.Length > 0 && char.IsLetter(n[0])) return n.Substring(0, 1).ToUpperInvariant();
            if (n.Length > 0 && char.IsDigit(n[0])) return "#";
            return "?";
        }
    }

    public string LastDateDisplay
    {
        get
        {
            if (LastDate == DateTime.MinValue) return "";
            var d = LastDate;
            var today = DateTime.Today;
            if (d.Date == today) return d.ToString("HH:mm");
            if (d.Date == today.AddDays(-1)) return "Yesterday";
            if ((today - d.Date).TotalDays < 7) return d.ToString("ddd");
            if (d.Year == today.Year) return d.ToString("MMM d");
            return d.ToString("yyyy-MM-dd");
        }
    }

    public string Preview
    {
        get
        {
            var m = LastMessage ?? "";
            if (m.Length > 60) m = m.Substring(0, 60) + "...";
            return m.Replace("\n", " ").Replace("\r", " ");
        }
    }

    public bool HasUnread => UnreadCount > 0;

    private static readonly string[] Palette =
    {
        "#4F8CFF", "#34D399", "#F59E0B", "#EC4899",
        "#A855F7", "#22D3EE", "#EF4444", "#818CF8"
    };

    public Brush AvatarBrush
    {
        get
        {
            int h = Math.Abs((Address ?? "").GetHashCode());
            var hex = Palette[h % Palette.Length];
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
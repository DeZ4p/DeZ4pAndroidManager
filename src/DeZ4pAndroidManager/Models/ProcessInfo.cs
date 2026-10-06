// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

public class ProcessInfo
{
    public string User { get; set; } = "";
    public int Pid { get; set; }
    public int Ppid { get; set; }
    public long VszKb { get; set; }
    public long RssKb { get; set; }
    public string State { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";

    public string VszDisplay => MediaItem.FormatSize(VszKb * 1024);
    public string RssDisplay => MediaItem.FormatSize(RssKb * 1024);

    public bool IsSystem =>
        User == "root" || User == "system" || User == "shell" ||
        User.StartsWith("u0_a", StringComparison.OrdinalIgnoreCase) == false;

    public bool IsUserApp =>
        User.StartsWith("u0_a", StringComparison.OrdinalIgnoreCase) ||
        User.StartsWith("u10", StringComparison.OrdinalIgnoreCase);

    public Brush UserColor
    {
        get
        {
            var hex = User switch
            {
                "root" => "#EF4444",
                "system" => "#F59E0B",
                "shell" => "#22D3EE",
                "radio" => "#A855F7",
                "nobody" => "#6B7280",
                _ when IsUserApp => "#34D399",
                _ => "#9AA0A6"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    /// <summary>Friendly package name if detected from process name.</summary>
    public string FriendlyName
    {
        get
        {
            var n = Name ?? "";
            if (n.Contains("."))
            {
                var parts = n.Split('.');
                if (parts.Length >= 2) return $"{parts[^2]}.{parts[^1]}";
            }
            return n;
        }
    }
}
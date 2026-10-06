// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Globalization;
using System.Windows.Media;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.Models;

public enum MediaKind { Image, Video, Audio, Other }

public class MediaItem
{
    public string Id { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.MinValue;
    public MediaKind Kind { get; set; } = MediaKind.Other;
    public string MimeType { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public long DurationMs { get; set; }

    public string SizeDisplay => FormatSize(SizeBytes);

    public string DateDisplay => DateAdded == DateTime.MinValue
        ? "-" : DateAdded.ToString("yyyy-MM-dd HH:mm");

    public string DimensionsDisplay
    {
        get
        {
            if (Kind == MediaKind.Image || Kind == MediaKind.Video)
            {
                if (Width > 0 && Height > 0) return $"{Width} × {Height}";
            }
            if (Kind == MediaKind.Audio && DurationMs > 0)
            {
                var ts = TimeSpan.FromMilliseconds(DurationMs);
                return ts.Hours > 0
                    ? $"{ts.Hours}:{ts.Minutes:00}:{ts.Seconds:00}"
                    : $"{ts.Minutes}:{ts.Seconds:00}";
            }
            return "-";
        }
    }

    public string KindDisplay => Kind switch
    {
        MediaKind.Image => LocalizationService.Translate("Media.KindImage"),
        MediaKind.Video => LocalizationService.Translate("Media.KindVideo"),
        MediaKind.Audio => LocalizationService.Translate("Media.KindAudio"),
        _ => "-"
    };

    public string IconGlyph => Kind switch
    {
        MediaKind.Image => "\uEB9F",
        MediaKind.Video => "\uE714",
        MediaKind.Audio => "\uE8D6",
        _ => "\uE7C3"
    };

    public Brush IconColor => Kind switch
    {
        MediaKind.Image => MakeBrush("#EC4899"),
        MediaKind.Video => MakeBrush("#A855F7"),
        MediaKind.Audio => MakeBrush("#22D3EE"),
        _ => MakeBrush("#6B7280")
    };

    private static Brush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "-";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.0} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.00} GB";
    }
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows.Media;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Represents a file or folder on the Android device.
/// </summary>
public class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime Modified { get; set; } = DateTime.MinValue;
    public bool IsDirectory { get; set; }
    public bool IsSymlink { get; set; }
    public string Permissions { get; set; } = "";

    public string SizeDisplay => IsDirectory ? "-" : FormatSize(SizeBytes);

    public string TypeDisplay => IsDirectory
        ? LocalizationService.Translate("FileManager.FolderType")
        : GetFileType(Name);

    public string ModifiedDisplay => Modified == DateTime.MinValue
        ? "-"
        : Modified.ToString("yyyy-MM-dd HH:mm");

    public string IconGlyph => GetIconGlyph();
    public Brush IconColor => GetIconColor();

    private string GetIconGlyph()
    {
        if (IsDirectory) return "\uE8B7";
        var ext = GetExtension(Name).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".md" or ".log" or ".ini" or ".conf" or ".xml" or ".json" => "\uE8A5",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\uEB9F",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".3gp" => "\uE714",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" => "\uE8D6",
            ".apk" or ".apks" or ".xapk" => "\uE7B8",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "\uF012",
            ".pdf" => "\uEA90",
            ".doc" or ".docx" => "\uE8A5",
            ".xls" or ".xlsx" => "\uE9F9",
            ".ppt" or ".pptx" => "\uE7F4",
            _ => "\uE7C3"
        };
    }

    private Brush GetIconColor()
    {
        if (IsDirectory) return MakeBrush("#F59E0B");
        var ext = GetExtension(Name).ToLowerInvariant();
        var hex = ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "#EC4899",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".3gp" => "#A855F7",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" => "#22D3EE",
            ".apk" or ".apks" or ".xapk" => "#34D399",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "#FBBF24",
            ".pdf" => "#EF4444",
            ".txt" or ".md" or ".log" or ".xml" or ".json" => "#9AA0A6",
            _ => "#6B7280"
        };
        return MakeBrush(hex);
    }

    private static Brush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static string GetExtension(string name)
    {
        var i = name.LastIndexOf('.');
        return i < 0 ? "" : name.Substring(i);
    }

    private static string GetFileType(string name)
    {
        var ext = GetExtension(name).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrEmpty(ext) ? LocalizationService.Translate("FileManager.FileType") : $"{ext}";
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
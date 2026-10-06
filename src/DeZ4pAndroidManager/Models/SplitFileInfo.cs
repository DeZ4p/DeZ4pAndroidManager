// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.Models;

public enum SplitFileType { Base, Native, Language, Density, Feature, Config, Unknown }

public class SplitFileInfo
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public SplitFileType Type { get; set; } = SplitFileType.Unknown;
    public string TypeLabel { get; set; } = "";
    public List<string> Architectures { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public List<string> Densities { get; set; } = new();
    public int DexCount { get; set; }
    public int NativeLibCount { get; set; }
    public int TotalEntries { get; set; }

    public string SizeDisplay => MediaItem.FormatSize(SizeBytes);

    public string TypeColor => Type switch
    {
        SplitFileType.Base => "#4F8CFF",
        SplitFileType.Native => "#A855F7",
        SplitFileType.Language => "#34D399",
        SplitFileType.Density => "#F59E0B",
        SplitFileType.Feature => "#EC4899",
        SplitFileType.Config => "#818CF8",
        _ => "#94A3B8"
    };

    public string TypeBadgeLabel => Type switch
    {
        SplitFileType.Base => "BASE",
        SplitFileType.Native => "ARCH",
        SplitFileType.Language => "LANG",
        SplitFileType.Density => "DPI",
        SplitFileType.Feature => "FEATURE",
        SplitFileType.Config => "CONFIG",
        _ => "?"
    };

    public string DetailsDisplay
    {
        get
        {
            var parts = new List<string>();
            if (Architectures.Count > 0) parts.Add(string.Join(", ", Architectures));
            if (Languages.Count > 0) parts.Add(string.Join(", ", Languages));
            if (Densities.Count > 0) parts.Add(string.Join(", ", Densities));
            if (DexCount > 0) parts.Add($"{DexCount} dex");
            if (NativeLibCount > 0) parts.Add($"{NativeLibCount} libs");
            if (TotalEntries > 0 && parts.Count == 0) parts.Add($"{TotalEntries} files");
            return parts.Count == 0 ? "-" : string.Join("  -  ", parts);
        }
    }

    public Brush TypeBrush
    {
        get
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(TypeColor));
            b.Freeze();
            return b;
        }
    }
}
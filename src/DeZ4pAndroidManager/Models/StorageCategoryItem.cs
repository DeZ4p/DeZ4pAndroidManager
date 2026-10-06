// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Media;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.Models;

public class StorageCategoryItem
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public double PercentOfTotal { get; set; }
    public string Icon { get; set; } = "\uE8B7";
    public string ColorHex { get; set; } = "#4F8CFF";

    public string SizeDisplay => MediaItem.FormatSize(SizeBytes);
    public string PercentDisplay => $"{PercentOfTotal:0.0}%";
    public string CountDisplay => FileCount <= 0 ? "â€”" : $"{FileCount:N0}";

    public Brush ColorBrush
    {
        get
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));
            b.Freeze(); return b;
        }
    }
}

public class LargestFile
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public string SizeDisplay => MediaItem.FormatSize(SizeBytes);
}
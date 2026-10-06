// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Media;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Represents an installed application package on the Android device.
/// </summary>
public class AppItem
{
    public string PackageName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VersionName { get; set; } = "";
    public bool IsSystem { get; set; }
    public bool IsEnabled { get; set; } = true;

    public string TypeDisplay => IsSystem
        ? LocalizationService.Translate("AppsManager.TypeSystem")
        : LocalizationService.Translate("AppsManager.TypeUser");

    public string StatusDisplay => IsEnabled
        ? LocalizationService.Translate("AppsManager.StatusEnabled")
        : LocalizationService.Translate("AppsManager.StatusDisabled");

    public string VersionDisplay => string.IsNullOrWhiteSpace(VersionName) ? "-" : VersionName;

    public string IconGlyph => IsSystem ? "\uE770" : "\uE71D";

    public Brush IconColor => MakeBrush(IsSystem ? "#F59E0B" : "#34D399");
    public Brush StatusColor => MakeBrush(IsEnabled ? "#34D399" : "#EF4444");
    public Brush TypeColor => MakeBrush(IsSystem ? "#A855F7" : "#22D3EE");

    private static Brush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
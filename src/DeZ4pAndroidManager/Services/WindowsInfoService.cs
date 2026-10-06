// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using Microsoft.Win32;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Detects the true Windows marketing name (Windows 10 vs Windows 11)
/// and edition (Home / Pro / Enterprise) using registry + build number.
/// Environment.OSVersion only returns 10.0.xxxxx on both - this fixes it.
/// </summary>
public static class WindowsInfoService
{
    /// <summary>
    /// Returns "Windows 11 Pro", "Windows 10 Home", etc.
    /// Falls back to "Windows 10" or "Windows" if detection fails.
    /// </summary>
    public static string GetFriendlyName()
    {
        try
        {
            // Read ProductName (e.g. "Windows 10 Pro")
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key == null) return "Windows";

            string productName = (key.GetValue("ProductName") as string) ?? "Windows";
            string displayVersion = (key.GetValue("DisplayVersion") as string) ?? "";
            string? buildStr = key.GetValue("CurrentBuildNumber") as string;

            // Windows 11 = build >= 22000; ProductName still says "Windows 10"
            if (int.TryParse(buildStr, out int build) && build >= 22000
                && productName.StartsWith("Windows 10", System.StringComparison.Ordinal))
            {
                productName = productName.Replace("Windows 10", "Windows 11");
            }

            return productName;
        }
        catch
        {
            return "Windows";
        }
    }

    /// <summary>
    /// Returns the OS build number (e.g. "22631").
    /// </summary>
    public static string GetBuildNumber()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return (key?.GetValue("CurrentBuildNumber") as string) ?? "?";
        }
        catch { return "?"; }
    }
}
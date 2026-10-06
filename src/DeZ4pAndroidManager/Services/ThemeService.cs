// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Detects Windows theme (Dark/Light) with fallbacks for Win10 1809+ / Win11.
/// Also applies the theme to app resources AND the native title bar.
/// </summary>
public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsSystemLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            object? value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue) return intValue != 0;

            value = key?.GetValue("SystemUsesLightTheme");
            if (value is int sysValue) return sysValue != 0;
        }
        catch { }

        return true;
    }

    public static void ApplyTheme(bool isLight)
    {
        if (Application.Current == null) return;

        string themeFile = isLight ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
        var newDict = new ResourceDictionary
        {
            Source = new Uri(themeFile, UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source != null &&
            (d.Source.OriginalString.Contains("LightTheme") ||
             d.Source.OriginalString.Contains("DarkTheme")));

        if (existing != null) merged.Remove(existing);
        merged.Add(newDict);

        // Apply to all open windows' title bars
        foreach (Window w in Application.Current.Windows)
        {
            // 0x00RRGGBB format for caption (Win11 only): use theme-consistent color
            int captionColor = isLight
                ? unchecked((int)0x00FAF5F5)  // light gray-blue
                : unchecked((int)0x00151A1A); // dark sidebar color

            WindowChromeService.ApplyTitleBarTheme(w, isDark: !isLight, captionColorHex: captionColor);
        }
    }

    public static void ApplySystemTheme() => ApplyTheme(IsSystemLightTheme());
}
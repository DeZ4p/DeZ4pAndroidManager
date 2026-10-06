// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Runtime language switching between English and Persian.
/// Persists choice in HKCU registry (user scope only).
/// </summary>
public static class LocalizationService
{
    private const string RegPath = @"Software\DeZ4pAndroidManager";
    private const string RegValue = "Language";

    /// <summary>Supported language codes - keep in sync with Localization/Strings.*.xaml.</summary>
    private static readonly string[] Supported = { "en", "fa" };

    public static string CurrentLanguage { get; private set; } = "en";
    public static bool IsRtl { get; private set; } = false;

    public static event EventHandler? LanguageChanged;

    public static void Initialize()
    {
        string code = LoadFromRegistry() ?? DetectFromSystem();
        Apply(code, raise: false);
    }

    public static void Apply(string code, bool raise = true)
    {
        if (string.IsNullOrWhiteSpace(code)) code = "en";
        if (!Supported.Contains(code)) code = "en";

        string resourcePath = $"Localization/Strings.{code}.xaml";
        ResourceDictionary newDict;
        try
        {
            newDict = new ResourceDictionary { Source = new Uri(resourcePath, UriKind.Relative) };
        }
        catch
        {
            code = "en";
            newDict = new ResourceDictionary { Source = new Uri("Localization/Strings.en.xaml", UriKind.Relative) };
        }

        if (Application.Current == null) return;

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.Contains("Strings."));
        if (existing != null) merged.Remove(existing);
        merged.Add(newDict);

        CurrentLanguage = code;
        IsRtl = code == "fa";

        SaveToRegistry(code);

        if (raise) LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Translate(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        if (Application.Current == null) return key;
        return Application.Current.TryFindResource(key) as string ?? key;
    }

    private static string DetectFromSystem()
    {
        try
        {
            string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return iso == "fa" ? "fa" : "en";
        }
        catch { return "en"; }
    }

    private static string? LoadFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            return key?.GetValue(RegValue) as string;
        }
        catch { return null; }
    }

    private static void SaveToRegistry(string code)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegPath);
            key?.SetValue(RegValue, code);
        }
        catch { /* silent */ }
    }
}
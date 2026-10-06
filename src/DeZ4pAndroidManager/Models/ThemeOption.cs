// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.ComponentModel;
using System.Runtime.CompilerServices;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Theme option for the Settings dropdown - supports runtime translation.
/// </summary>
public class ThemeOption : INotifyPropertyChanged
{
    public ThemeOption(string key, string labelKey, string icon)
    {
        Key = key;
        LabelKey = labelKey;
        Icon = icon;
        LocalizationService.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public string Key { get; }
    public string LabelKey { get; }
    public string Icon { get; }

    /// <summary>Translated label - re-evaluated on language change.</summary>
    public string Label => LocalizationService.Translate(LabelKey);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
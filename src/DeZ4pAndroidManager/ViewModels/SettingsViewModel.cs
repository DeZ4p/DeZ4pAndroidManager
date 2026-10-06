// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private LanguageItem? _selectedLanguage;
    private ThemeOption? _selectedTheme;
    private bool _suppressCallbacks;

    public SettingsViewModel()
    {
        _suppressCallbacks = true;

        // ─── Languages ───
        Languages = new ObservableCollection<LanguageItem>
        {
            new("en", "EN", "#2563EB", "English", "English", false),
            new("fa", "FA", "#22C55E", "فارسی",   "Persian", true),
        };
        MarkActiveLanguage();
        _selectedLanguage = Languages.FirstOrDefault(l => l.IsActive) ?? Languages[0];

        // ─── Themes ───
        Themes = new ObservableCollection<ThemeOption>
        {
            new("dark",   "Settings.Dark",   "\uE708"),
            new("light",  "Settings.Light",  "\uE706"),
            new("system", "Settings.System", "\uE7F8"),
        };

        // Detect current theme
        bool isLight = ThemeService.IsSystemLightTheme();
        // "system" is the default → but we track the current from what was applied
        // Since ThemeService.ApplySystemTheme was called on startup, we default to "system"
        _selectedTheme = Themes.FirstOrDefault(t => t.Key == "system") ?? Themes[2];

        _suppressCallbacks = false;

        // ─── React to language change ───
        LocalizationService.LanguageChanged += (_, _) =>
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                _suppressCallbacks = true;
                MarkActiveLanguage();
                _suppressCallbacks = false;
            });
        };
    }

    public ObservableCollection<LanguageItem> Languages { get; }
    public ObservableCollection<ThemeOption> Themes { get; }

    public LanguageItem? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value == null || _selectedLanguage == value) return;
            _selectedLanguage = value;
            OnPropertyChanged();
            if (_suppressCallbacks) return;

            LocalizationService.Apply(value.Code);
            MarkActiveLanguage();
        }
    }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (value == null || _selectedTheme == value) return;
            _selectedTheme = value;
            OnPropertyChanged();
            if (_suppressCallbacks) return;

            switch (value.Key)
            {
                case "dark":   ThemeService.ApplyTheme(isLight: false); break;
                case "light":  ThemeService.ApplyTheme(isLight: true);  break;
                case "system": ThemeService.ApplySystemTheme();         break;
            }
        }
    }

    private void MarkActiveLanguage()
    {
        var current = LocalizationService.CurrentLanguage;
        foreach (var lang in Languages)
            lang.IsActive = lang.Code == current;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Language option for the Settings dropdown - colored badge + native name.
/// </summary>
public class LanguageItem : INotifyPropertyChanged
{
    private bool _isActive;

    public LanguageItem(string code, string badgeText, string badgeColorHex,
                         string nativeName, string englishName, bool isRtl)
    {
        Code = code;
        BadgeText = badgeText;
        NativeName = nativeName;
        EnglishName = englishName;
        IsRtl = isRtl;

        var color = (Color)ColorConverter.ConvertFromString(badgeColorHex);
        BadgeBrush = new SolidColorBrush(color);
        BadgeBrush.Freeze();
    }

    public string Code { get; }
    public string BadgeText { get; }
    public string NativeName { get; }
    public string EnglishName { get; }
    public bool IsRtl { get; }
    public SolidColorBrush BadgeBrush { get; }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
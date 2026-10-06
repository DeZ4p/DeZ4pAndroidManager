// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeZ4pAndroidManager.Models;

public enum BackupCategoryKind
{
    Contacts,
    Sms,
    CallLogs,
    Media,
    AppsList,
    Apks,
    WhatsApp
}

/// <summary>One selectable category in the backup / restore UI.</summary>
public class BackupCategory : INotifyPropertyChanged
{
    private bool _isSelected;

    public BackupCategoryKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "\uE8B7";
    public string Color { get; set; } = "#4F8CFF";

    // Runtime stats (filled during/after backup)
    private int _itemCount;
    private long _bytes;
    private bool _isAvailable = true;
    private string _statusNote = "";
    private double _progress;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public int ItemCount
    {
        get => _itemCount;
        set { _itemCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ItemCountDisplay)); }
    }

    public long Bytes
    {
        get => _bytes;
        set { _bytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(BytesDisplay)); }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set { _isAvailable = value; OnPropertyChanged(); }
    }

    public string StatusNote
    {
        get => _statusNote;
        set { _statusNote = value ?? ""; OnPropertyChanged(); }
    }

    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string ItemCountDisplay => _itemCount <= 0 ? "-" : _itemCount.ToString();
    public string BytesDisplay => _bytes <= 0 ? "-" : MediaItem.FormatSize(_bytes);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
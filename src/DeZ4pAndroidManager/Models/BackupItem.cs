// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeZ4pAndroidManager.Models;

public enum BackupStatus { Pending, Running, Done, Failed, Cancelled }

public class BackupItem : INotifyPropertyChanged
{
    private BackupStatus _status = BackupStatus.Pending;
    private double _progress;
    private string _errorMessage = "";
    private string _localPath = "";
    private long _totalBytes;

    public string PackageName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VersionName { get; set; } = "";
    public bool IsSystem { get; set; }
    public int ApkCount { get; set; }
    public int ApkDone { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.MinValue;
    public DateTime FinishedAt { get; set; } = DateTime.MinValue;

    public BackupStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); OnPropertyChanged(nameof(StatusColor)); }
    }

    public double Progress
    {
        get => _progress;
        set { _progress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); OnPropertyChanged(nameof(ProgressDisplay)); }
    }

    public string ProgressDisplay => $"{_progress:0}%";

    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value ?? ""; OnPropertyChanged(); }
    }

    public string LocalPath
    {
        get => _localPath;
        set { _localPath = value ?? ""; OnPropertyChanged(); }
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); }
    }

    public string SizeDisplay => TotalBytes <= 0 ? "-" : MediaItem.FormatSize(TotalBytes);

    public string StatusDisplay => Status switch
    {
        BackupStatus.Pending => "Pending",
        BackupStatus.Running => "Running",
        BackupStatus.Done => "Done",
        BackupStatus.Failed => "Failed",
        BackupStatus.Cancelled => "Cancelled",
        _ => "-"
    };

    public string StatusColor => Status switch
    {
        BackupStatus.Pending => "#94A3B8",
        BackupStatus.Running => "#4F8CFF",
        BackupStatus.Done => "#34D399",
        BackupStatus.Failed => "#EF4444",
        BackupStatus.Cancelled => "#F59E0B",
        _ => "#94A3B8"
    };

    public string ApkCountDisplay => ApkCount <= 0 ? "-" : $"{ApkDone}/{ApkCount}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
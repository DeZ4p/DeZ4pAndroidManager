// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeZ4pAndroidManager.Models;

public class ScheduledTask : INotifyPropertyChanged
{
    private bool _isEnabled;
    private DateTime _lastRun = DateTime.MinValue;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public int IntervalSeconds { get; set; } = 60;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }
    public DateTime LastRun { get => _lastRun; set { _lastRun = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastRunDisplay)); } }
    public string LastResult { get; set; } = "";

    public string LastRunDisplay => LastRun == DateTime.MinValue ? "never" : LastRun.ToString("HH:mm:ss");
    public string IntervalDisplay => IntervalSeconds < 60 ? $"{IntervalSeconds}s" : $"{IntervalSeconds / 60}m";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace DeZ4pAndroidManager.Models;

public class FlashImageItem : INotifyPropertyChanged
{
    private string _status = "Pending";
    private double _progress;
    private long _durationMs;

    public string Partition { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string ImageName => string.IsNullOrEmpty(ImagePath) ? "" : System.IO.Path.GetFileName(ImagePath);
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
    public long DurationMs { get => _durationMs; set { _durationMs = value; OnPropertyChanged(); } }
    public string Slot { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
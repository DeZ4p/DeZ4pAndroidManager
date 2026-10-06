// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeZ4pAndroidManager.Models;

public class PermissionApp : INotifyPropertyChanged
{
    private bool _isSelected;
    public string PackageName { get; set; } = "";
    public string AppName { get; set; } = "";
    public int DangerousCount { get; set; }
    public ObservableCollection<string> GrantedPermissions { get; } = new();
    public ObservableCollection<string> DeniedPermissions { get; } = new();

    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public string DangerousDisplay => DangerousCount <= 0 ? "Safe" : $"{DangerousCount} risk";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
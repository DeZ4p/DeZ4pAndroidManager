// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Global state - tracks the currently-active device (ADB or fastboot).
/// </summary>
public class DeviceStateService : INotifyPropertyChanged
{
    private DeviceModel? _currentDevice;

    public DeviceModel? CurrentDevice
    {
        get => _currentDevice;
        set
        {
            bool was = HasDevice;
            bool wasFastboot = _currentDevice?.IsFastboot ?? false;

            _currentDevice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(IsFastbootMode));

            if (was != HasDevice || wasFastboot != IsFastbootMode)
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasDevice => CurrentDevice != null;
    public bool IsFastbootMode => CurrentDevice?.IsFastboot ?? false;

    public event EventHandler? ConnectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
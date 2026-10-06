// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class DevicesViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly FastbootService _fastboot;
    private readonly DeviceWatcherService _watcher;

    private DeviceModel? _selectedDevice;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public DevicesViewModel(AdbService adb, FastbootService fastboot, DeviceWatcherService watcher)
    {
        _adb = adb;
        _fastboot = fastboot;
        _watcher = watcher;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        // Auto-refresh list when watcher fires
        _watcher.DevicesChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(() => LoadFromWatcher());
        };

        LoadFromWatcher();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool IsAdbAvailable => _adb.IsAdbAvailable;

    public ICommand RefreshCommand { get; }

    private void LoadFromWatcher()
    {
        var list = _watcher.CurrentDevices;

        Devices.Clear();
        foreach (var d in list) Devices.Add(d);

        int ready = list.Count(d => d.IsReady);
        int fastboot = list.Count(d => d.IsFastboot);

        if (list.Count == 0)
            StatusMessage = "No devices connected.";
        else if (fastboot > 0 && ready == fastboot)
            StatusMessage = $"{fastboot} device(s) in Fastboot mode.";
        else
            StatusMessage = ready switch
            {
                0 => "No devices ready.",
                1 => "1 device found.",
                _ => $"{ready} devices found."
            };
    }

    public async Task InitializeAsync()
    {
        await _watcher.ForceCheckAsync();
        LoadFromWatcher();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _watcher.ForceCheckAsync();
            LoadFromWatcher();
        }
        catch (Exception ex) { StatusMessage = $"Refresh error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class RecoveryManagerViewModel : INotifyPropertyChanged
{
    private readonly RecoveryManagerService _svc;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private DeviceModel? _selectedDevice;
    private string _currentMode = "";
    private bool _isBusy;
    private string _statusMessage = "Ready.";

    public RecoveryManagerViewModel(RecoveryManagerService svc, DeviceStateService deviceState,
                                     DeviceWatcherService watcher, ActivityLogService activity)
    {
        _svc = svc;
        _deviceState = deviceState;
        _activity = activity;

        BootTargets = new ObservableCollection<BootTarget>(RecoveryManagerService.GetBootTargets());

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ExecuteCommand = new AsyncRelayCommand<BootTarget>(ExecuteAsync);
        ClearHistoryCommand = new RelayCommand(_ => History.Clear());

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        };

        watcher.DevicesChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        };

        _ = RefreshAsync();
    }

    public ObservableCollection<BootTarget> BootTargets { get; }
    public ObservableCollection<BootHistoryEntry> History { get; } = new();
    public ObservableCollection<DeviceModel> Devices { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); OnPropertyChanged(nameof(DeviceLabel)); }
    }

    public bool HasDevice => _selectedDevice != null;
    public string DeviceLabel => _selectedDevice?.DisplayName ?? "No device";

    public string CurrentMode
    {
        get => _currentMode;
        set { _currentMode = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(ModeBadge)); OnPropertyChanged(nameof(ModeColor)); }
    }

    public string ModeBadge => string.IsNullOrEmpty(CurrentMode) ? "-" : CurrentMode switch
    {
        "adb" => "ADB (Normal)",
        "recovery" => "ADB Recovery",
        "fastboot" => "Fastboot",
        "fastbootd" => "Fastbootd",
        "unauthorized" => "Unauthorized",
        _ => CurrentMode
    };

    public string ModeColor => CurrentMode switch
    {
        "adb" => "#34D399",
        "recovery" => "#F59E0B",
        "fastboot" => "#EF4444",
        "fastbootd" => "#818CF8",
        "unauthorized" => "#F59E0B",
        _ => "#6B7280"
    };

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExecuteCommand { get; }
    public ICommand ClearHistoryCommand { get; }

    private async Task RefreshAsync()
    {
        var cur = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var w = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (w != null)
                foreach (var d in w.CurrentDevices.Where(x => x.IsReady))
                    Devices.Add(d);
        }
        catch { }

        if (!string.IsNullOrEmpty(cur))
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];

        var d2 = _deviceState.CurrentDevice;
        if (d2 != null)
        {
            if (d2.IsFastboot)
                CurrentMode = d2.State?.Contains("fastbootd") == true ? "fastbootd" : "fastboot";
            else
                CurrentMode = d2.State switch
                {
                    "device" => "adb",
                    "recovery" => "recovery",
                    "unauthorized" => "unauthorized",
                    _ => "adb"
                };
        }
        else
        {
            CurrentMode = "";
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteAsync(BootTarget? target)
    {
        if (target == null) return;
        if (SelectedDevice == null) { StatusMessage = "No device selected."; return; }

        if (target.IsDestructive)
        {
            var r = MessageBox.Show(
                $"⚠ این عملیات خطرناک است:\n\n{target.Label}\n{target.Description}\n\nادامه؟",
                "Confirm Destructive Action",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = $"Sending: {target.Label}...";
            _activity.LogReboot($"{target.Label} on {SelectedDevice.DisplayName}");

            var (ok, msg) = await _svc.ExecuteAsync(SelectedDevice.Serial, target, CurrentMode);

            History.Insert(0, new BootHistoryEntry
            {
                Target = target.Label,
                Mode = CurrentMode,
                Time = DateTime.Now,
                Success = ok,
                Message = msg
            });

            while (History.Count > 50) History.RemoveAt(History.Count - 1);

            StatusMessage = ok ? $"✓ {target.Label} sent" : $"✗ {target.Label} failed: {msg}";

            if (!ok && !string.IsNullOrWhiteSpace(msg))
                MessageBox.Show($"Failed to send command:\n\n{msg}", "Boot Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

            // Refresh after a short delay
            await Task.Delay(1500);
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
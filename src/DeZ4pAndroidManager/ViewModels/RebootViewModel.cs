// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

/// <summary>
/// Reboot page - mode-aware. Uses fastboot commands when device is in
/// bootloader/fastbootd mode, ADB commands when running Android.
/// </summary>
public class RebootViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly FastbootService _fastboot;
    private readonly DeviceWatcherService _watcher;
    private readonly ActivityLogService _activity;

    private DeviceModel? _selectedDevice;
    private string _statusMessage = LocalizationService.Translate("Reboot.SelectDevice");

    public RebootViewModel(AdbService adb, FastbootService fastboot,
                            DeviceWatcherService watcher, ActivityLogService activity)
    {
        _adb = adb;
        _fastboot = fastboot;
        _watcher = watcher;
        _activity = activity;

        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
        RebootCommand = new AsyncRelayCommand<string>(RebootAsync);

        // Auto-populate device list from watcher
        _watcher.DevicesChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(RefreshDevicesFromWatcher);
        };

        // Re-translate default message when language changes
        LocalizationService.LanguageChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(_statusMessage)) return;
                var en = "Select a device and choose a reboot action.";
                var fa = "یک دستگاه انتخاب کنید و عملیات ری‌استارت را برگزینید.";
                if (_statusMessage == en || _statusMessage == fa)
                    StatusMessage = LocalizationService.Translate("Reboot.SelectDevice");
            });
        };

        // Immediate first fill
        RefreshDevicesFromWatcher();
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

    public ICommand RefreshDevicesCommand { get; }
    public ICommand RebootCommand { get; }

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
    }

    public Task RefreshDevicesAsync() => _watcher.ForceCheckAsync();

    private void RefreshDevicesFromWatcher()
    {
        var list = _watcher.CurrentDevices;

        Devices.Clear();
        foreach (var d in list) Devices.Add(d);

        if (SelectedDevice != null)
        {
            var still = Devices.FirstOrDefault(d => d.Serial == SelectedDevice.Serial);
            SelectedDevice = still;
        }

        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];
    }

    private async Task RebootAsync(string? mode)
    {
        // ⭐ Capture the device once - guarantees non-null through the whole method
        var device = SelectedDevice;

        if (device == null)
        {
            StatusMessage = "Select a device first.";
            return;
        }

        if (string.IsNullOrEmpty(mode)) return;

        // ═══ FASTBOOT MODE ═══
        if (device.IsFastboot)
        {
            await RebootFastbootAsync(device, mode);
            return;
        }

        // ═══ ADB MODE ═══
        await RebootAdbAsync(device, mode);
    }

    private async Task RebootAdbAsync(DeviceModel device, string mode)
    {
        string command = mode switch
        {
            "normal"     => "reboot",
            "recovery"   => "reboot recovery",
            "bootloader" => "reboot bootloader",
            "fastboot"   => "reboot fastboot",
            "shutdown"   => "shell reboot -p",
            "edl"        => "reboot edl",
            "systemui"   => "shell pkill -f com.android.systemui",
            _            => "reboot"
        };

        StatusMessage = $"Sending '{command}' via ADB...";
        try
        {
            var result = await _adb.ExecuteRawAsync($"-s {device.Serial} {command}");
            if (result.ExitCode == 0)
            {
                StatusMessage = $"✅ Command sent: {command}";
                _activity.LogReboot($"Reboot ({mode}): {device.DisplayName}");
            }
            else
            {
                StatusMessage = $"❌ Failed: {result.StandardError}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
    }

    private async Task RebootFastbootAsync(DeviceModel device, string mode)
    {
        StatusMessage = "Sending fastboot command...";
        try
        {
            RawResult result;
            string label;

            switch (mode)
            {
                case "normal":
                    result = await _fastboot.RebootAsync(device.Serial);
                    label = "fastboot reboot";
                    break;

                case "recovery":
                    result = await _fastboot.RebootRecoveryAsync(device.Serial);
                    label = "fastboot reboot recovery";
                    break;

                case "bootloader":
                    result = await _fastboot.RebootBootloaderAsync(device.Serial);
                    label = "fastboot reboot-bootloader";
                    break;

                case "fastboot":
                    result = await _fastboot.RebootFastbootdAsync(device.Serial);
                    label = "fastboot reboot fastboot";
                    break;

                case "shutdown":
                    result = await _fastboot.ShutdownAsync(device.Serial);
                    label = "fastboot oem poweroff";
                    break;

                case "systemui":
                    StatusMessage = "System UI restart requires ADB (device must be running Android).";
                    return;

                case "edl":
                    result = await _fastboot.ExecuteAsync(device.Serial, "oem edl");
                    label = "fastboot oem edl";
                    break;

                default:
                    result = await _fastboot.RebootAsync(device.Serial);
                    label = "fastboot reboot";
                    break;
            }

            if (result.ExitCode == 0)
            {
                StatusMessage = $"✅ Command sent: {label}";
                _activity.LogReboot($"Fastboot reboot ({mode}): {device.DisplayName}");
            }
            else
            {
                StatusMessage = $"❌ Failed: {result.StandardError}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class FlashToolsViewModel : INotifyPropertyChanged
{
    private readonly FlashService _svc;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private DeviceModel? _selectedDevice;
    private bool _isBusy;
    private bool _flashBothSlots;
    private bool _rebootAfter;
    private bool _skipVerify;
    private bool _autoAdvance;
    private string _statusMessage = "Ready.";
    private string _selectedPartition = "boot";
    private string _imagePath = "";
    private double _overallProgress;
    private string _currentSlot = "?";
    private long _lastDurationMs;
    private int _successCount;
    private int _failCount;

    public FlashToolsViewModel(FlashService svc, DeviceStateService deviceState,
                               DeviceWatcherService watcher, ActivityLogService activity)
    {
        _svc = svc;
        _deviceState = deviceState;
        _activity = activity;

        PickImageCommand = new RelayCommand(_ => PickImage());
        FlashCommand = new AsyncRelayCommand(FlashAsync);
        WipeCommand = new AsyncRelayCommand(WipeAsync);
        ClearHistoryCommand = new RelayCommand(_ => { History.Clear(); SuccessCount = 0; FailCount = 0; });
        RefreshCommand = new RelayCommand(_ => RefreshDevices());
        RetryFailedCommand = new AsyncRelayCommand(RetryFailedAsync);

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        RefreshDevices();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<FlashImageItem> History { get; } = new();
    public ObservableCollection<string> CommonPartitions { get; } = new()
    {
        "boot", "init_boot", "recovery", "system", "system_ext", "vendor",
        "vbmeta", "vbmeta_system", "vbmeta_vendor", "dtbo", "super",
        "product", "odm", "cache", "userdata", "modem", "bootloader", "radio"
    };

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFastboot)); OnPropertyChanged(nameof(HasDevice)); OnPropertyChanged(nameof(DeviceLabel)); }
    }
    public bool HasDevice => _selectedDevice != null;
    public bool IsFastboot => _selectedDevice?.IsFastboot == true;
    public string DeviceLabel => _selectedDevice?.DisplayName ?? "No device";

    public string SelectedPartition { get => _selectedPartition; set { _selectedPartition = value ?? ""; OnPropertyChanged(); } }
    public string ImagePath { get => _imagePath; set { _imagePath = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(ImageName)); OnPropertyChanged(nameof(HasImage)); } }
    public string ImageName => string.IsNullOrEmpty(ImagePath) ? "(no image selected)" : Path.GetFileName(ImagePath);
    public bool HasImage => File.Exists(ImagePath);

    public bool FlashBothSlots { get => _flashBothSlots; set { _flashBothSlots = value; OnPropertyChanged(); } }
    public bool RebootAfter { get => _rebootAfter; set { _rebootAfter = value; OnPropertyChanged(); } }
    public bool SkipVerify { get => _skipVerify; set { _skipVerify = value; OnPropertyChanged(); } }
    public bool AutoAdvance { get => _autoAdvance; set { _autoAdvance = value; OnPropertyChanged(); } }

    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public double OverallProgress { get => _overallProgress; set { _overallProgress = value; OnPropertyChanged(); } }
    public string CurrentSlot { get => _currentSlot; set { _currentSlot = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }
    public long LastDurationMs { get => _lastDurationMs; set { _lastDurationMs = value; OnPropertyChanged(); } }
    public int SuccessCount { get => _successCount; set { _successCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalRuns)); } }
    public int FailCount { get => _failCount; set { _failCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalRuns)); } }
    public int TotalRuns => _successCount + _failCount;

    public ICommand PickImageCommand { get; }
    public ICommand FlashCommand { get; }
    public ICommand WipeCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RetryFailedCommand { get; }

    private void RefreshDevices()
    {
        var cur = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var w = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (w != null) foreach (var d in w.CurrentDevices.Where(x => x.IsFastboot)) Devices.Add(d);
        }
        catch { }
        if (!string.IsNullOrEmpty(cur)) SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];
        _ = LoadSlotAsync();
    }

    private async Task LoadSlotAsync()
    {
        if (SelectedDevice == null) { CurrentSlot = "?"; return; }
        var (ok, slot) = await _svc.GetCurrentSlotAsync(SelectedDevice.Serial);
        CurrentSlot = ok ? slot : "?";
    }

    private void PickImage()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select image file",
            Filter = "Image files (*.img;*.bin)|*.img;*.bin|All files (*.*)|*.*"
        };
        if (ofd.ShowDialog() == true) ImagePath = ofd.FileName;
    }

    private async Task FlashAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "No fastboot device."; return; }
        if (!File.Exists(ImagePath)) { StatusMessage = "Select an image file first."; return; }

        var confirm = MessageBox.Show(
            $"Flash '{SelectedPartition}'?\n\nImage: {ImageName}\nBoth slots: {FlashBothSlots}\n\n" +
            "⚠ Wrong image can brick your device.",
            "Confirm Flash", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        await DoFlashAsync(SelectedPartition, ImagePath);
    }

    private async Task DoFlashAsync(string partition, string imagePath)
    {
        IsBusy = true; OverallProgress = 20;
        var item = new FlashImageItem
        {
            Partition = partition,
            ImagePath = imagePath,
            Status = "Flashing...",
            Slot = FlashBothSlots ? "all" : CurrentSlot
        };
        History.Insert(0, item);
        try
        {
            StatusMessage = $"Flashing {partition}...";
            OverallProgress = 60;
            var (ok, msg, ms) = await _svc.FlashAsync(SelectedDevice!.Serial, partition, imagePath, FlashBothSlots);

            item.Status = ok ? "Success" : "Failed";
            item.DurationMs = ms;
            item.Progress = 100;
            LastDurationMs = ms;

            if (ok) { SuccessCount++; StatusMessage = $"✓ {partition} flashed in {ms}ms"; _activity.LogInfo($"Flashed: {partition} ({ImageName})", "IMG", "#34D399"); }
            else { FailCount++; StatusMessage = $"✗ {msg}"; _activity.LogInfo($"Flash failed: {partition}", "IMG", "#EF4444"); }

            OverallProgress = 100;

            if (ok && RebootAfter)
            {
                await Task.Delay(800);
                await _svc.RebootAfterFlashAsync(SelectedDevice.Serial);
                StatusMessage += " — rebooting";
            }
        }
        finally { IsBusy = false; }
    }

    private async Task RetryFailedAsync()
    {
        var failed = History.FirstOrDefault(h => h.Status == "Failed");
        if (failed == null) { StatusMessage = "No failed flash to retry."; return; }
        if (SelectedDevice == null) return;
        History.Remove(failed);
        await DoFlashAsync(failed.Partition, failed.ImagePath);
    }

    private async Task WipeAsync()
    {
        if (SelectedDevice == null) return;
        var confirm = MessageBox.Show(
            $"Erase '{SelectedPartition}' partition?\n\n⚠ All data on this partition will be lost.",
            "Confirm Erase", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            StatusMessage = $"Erasing {SelectedPartition}...";
            var (ok, msg) = await _svc.WipeAsync(SelectedDevice.Serial, SelectedPartition);
            StatusMessage = ok ? $"✓ {SelectedPartition} erased" : $"✗ {msg}";
            if (ok) _activity.LogInfo($"Erased partition: {SelectedPartition}", "DEL", "#EF4444");
        }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
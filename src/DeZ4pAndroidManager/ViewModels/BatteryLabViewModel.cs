// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class BatteryLabViewModel : INotifyPropertyChanged
{
    private readonly BatteryLabService _svc;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private bool _isLoading;
    private string _statusMessage = "Ready.";
    private BatteryInfo? _info;

    public BatteryLabViewModel(BatteryLabService svc, DeviceStateService deviceState,
                                DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SaveReportCommand = new RelayCommand(_ => SaveReport());
        OpenFolderCommand = new RelayCommand(_ => AppPaths.OpenFolder(Path.Combine(AppPaths.Root, "Reports")));

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());

        _ = RefreshAsync();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }
    public bool HasDevice => _selectedDevice != null;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }

    public BatteryInfo? Info
    {
        get => _info;
        set { _info = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInfo)); }
    }
    public bool HasInfo => _info != null;

    public ICommand RefreshCommand { get; }
    public ICommand SaveReportCommand { get; }
    public ICommand OpenFolderCommand { get; }

    private async Task RefreshAsync()
    {
        var cur = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var w = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (w != null)
                foreach (var d in w.CurrentDevices.Where(x => x.IsReady && !x.IsFastboot))
                    Devices.Add(d);
        }
        catch { }

        if (!string.IsNullOrEmpty(cur)) SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];

        if (SelectedDevice == null) { StatusMessage = "No device."; Info = null; return; }

        IsLoading = true;
        try
        {
            StatusMessage = "Reading battery...";
            Info = await _svc.GetAsync(SelectedDevice.Serial);
            StatusMessage = $"Battery: {Info?.LevelDisplay} Â· {Info?.Status}";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void SaveReport()
    {
        if (_info == null) return;
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"battery_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Battery Lab Report");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine();
            sb.AppendLine($"Level:            {_info.LevelDisplay}");
            sb.AppendLine($"Status:           {_info.Status}");
            sb.AppendLine($"Health:           {_info.Health}");
            sb.AppendLine($"Temperature:      {_info.TempDisplay}");
            sb.AppendLine($"Voltage:          {_info.VoltageDisplay}");
            sb.AppendLine($"Current:          {_info.CurrentDisplay}");
            sb.AppendLine($"Technology:       {_info.Technology}");
            sb.AppendLine($"Charge source:    {_info.ChargeSource}");
            sb.AppendLine($"Capacity:         {_info.CapacityDisplay}");
            sb.AppendLine($"Health (calc):    {_info.HealthPercent}");
            sb.AppendLine($"Cycle count:      {_info.CycleDisplay}");
            sb.AppendLine($"Max charge cur:   {(_info.MaxChargingCurrentMa > 0 ? $"{_info.MaxChargingCurrentMa} mA" : "N/A")}");
            sb.AppendLine($"Max charge volt:  {(_info.MaxChargingVoltageMv > 0 ? $"{_info.MaxChargingVoltageMv / 1000.0:0.00} V" : "N/A")}");
            sb.AppendLine($"Power profile:    {_info.PowerProfile}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"âœ“ Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"âœ— {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
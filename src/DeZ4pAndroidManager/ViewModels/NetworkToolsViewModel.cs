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

public class NetworkToolsViewModel : INotifyPropertyChanged
{
    private readonly NetworkToolsService _svc;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private bool _isLoading;
    private string _statusMessage = "Ready.";
    private NetworkInfo? _info;

    public NetworkToolsViewModel(NetworkToolsService svc, DeviceStateService deviceState,
                                  DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        PingCommand = new AsyncRelayCommand(PingAsync);
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

    public NetworkInfo? Info
    {
        get => _info;
        set { _info = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInfo)); }
    }
    public bool HasInfo => _info != null;

    public ICommand RefreshCommand { get; }
    public ICommand PingCommand { get; }
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
            StatusMessage = "Reading network...";
            Info = await _svc.GetAsync(SelectedDevice.Serial);
            StatusMessage = $"WiFi: {Info?.WifiSsid} Â· {Info?.Interfaces.Count} interface(s)";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private async Task PingAsync()
    {
        if (SelectedDevice == null) return;
        IsLoading = true;
        try
        {
            StatusMessage = "Pinging 8.8.8.8...";
            var adb = App.Services.GetService(typeof(AdbService)) as AdbService;
            if (adb == null) return;
            var r = await adb.ShellAsync(SelectedDevice.Serial, "ping -c 4 8.8.8.8 2>&1");
            var lines = (r ?? "").Split('\n').Where(l => l.Contains("time=") || l.Contains("packet loss")).Take(5);
            StatusMessage = "Ping: " + string.Join(" | ", lines.Select(x => x.Trim()));
        }
        catch (Exception ex) { StatusMessage = $"âœ— {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void SaveReport()
    {
        if (_info == null) return;
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"network_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Network Report");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ WiFi â”€â”€");
            sb.AppendLine($"  SSID:        {_info.WifiSsid}");
            sb.AppendLine($"  IP:          {_info.WifiIp}");
            sb.AppendLine($"  MAC:         {_info.WifiMac}");
            sb.AppendLine($"  RSSI:        {_info.RssiDisplay}  ({_info.RssiQuality})");
            sb.AppendLine($"  Link speed:  {_info.LinkSpeedDisplay}");
            sb.AppendLine($"  Frequency:   {_info.FrequencyDisplay}");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ Mobile â”€â”€");
            sb.AppendLine($"  Operator:    {_info.MobileOperator}");
            sb.AppendLine($"  Network:     {_info.MobileNetworkType}");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ DNS / Route â”€â”€");
            sb.AppendLine($"  DNS 1:       {_info.Dns1}");
            sb.AppendLine($"  DNS 2:       {_info.Dns2}");
            sb.AppendLine($"  Gateway:     {_info.DefaultGateway}");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ Interfaces â”€â”€");
            foreach (var i in _info.Interfaces)
                sb.AppendLine($"  {i.Name,-8} {i.State,-6} IP={i.Ip,-16} MAC={i.Mac}  RX={i.RxDisplay}  TX={i.TxDisplay}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"âœ“ Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"âœ— {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
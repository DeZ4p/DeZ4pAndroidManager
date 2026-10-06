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

public class WirelessAdbViewModel : INotifyPropertyChanged
{
    private readonly WirelessAdbService _wifi;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private string _deviceIp = "";
    private int _tcpPort = 5555;
    private int _pairPort = 0;
    private string _pairCode = "";
    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private string _logText = "";

    private int _apiLevel = 0;
    private string _androidVersion = "?";
    private bool _isVersionChecked = false;

    public WirelessAdbViewModel(WirelessAdbService wifi, DeviceStateService deviceState,
                                 DeviceWatcherService watcher)
    {
        _wifi = wifi;
        _deviceState = deviceState;

        RefreshCommand = new AsyncRelayCommand(CheckDeviceAsync);
        GetIpCommand = new AsyncRelayCommand(GetIpAsync);
        EnableTcpIpCommand = new AsyncRelayCommand(EnableTcpIpAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
        PairCommand = new AsyncRelayCommand(PairAsync);
        ClearLogCommand = new RelayCommand(_ => LogText = "");
        FullAutoCommand = new AsyncRelayCommand(FullAutoAsync);

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() => _ = CheckDeviceAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() => _ = CheckDeviceAsync());

        _ = CheckDeviceAsync();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<string> PairedDevices { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); OnPropertyChanged(nameof(CanEnableTcpIp)); }
    }

    public bool HasDevice => _selectedDevice != null;

    public string DeviceIp
    {
        get => _deviceIp;
        set { _deviceIp = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(CanConnect)); }
    }

    public int TcpPort
    {
        get => _tcpPort;
        set { _tcpPort = Math.Max(1, Math.Min(65535, value)); OnPropertyChanged(); OnPropertyChanged(nameof(CanConnect)); }
    }

    public int PairPort
    {
        get => _pairPort;
        set { _pairPort = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanPair)); }
    }

    public string PairCode
    {
        get => _pairCode;
        set { _pairCode = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(CanPair)); }
    }

    public bool CanEnableTcpIp => HasDevice && IsVersionSupported;
    public bool CanConnect => !string.IsNullOrWhiteSpace(DeviceIp) && TcpPort > 0 && IsVersionSupported;
    public bool CanPair => !string.IsNullOrWhiteSpace(DeviceIp) && PairPort > 0 && !string.IsNullOrWhiteSpace(PairCode) && IsVersionSupported;

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

    public string LogText
    {
        get => _logText;
        set { _logText = value ?? ""; OnPropertyChanged(); }
    }

    public int ApiLevel
    {
        get => _apiLevel;
        set { _apiLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsVersionSupported)); OnPropertyChanged(nameof(HasVersionWarning)); OnPropertyChanged(nameof(VersionWarning)); OnPropertyChanged(nameof(CanEnableTcpIp)); OnPropertyChanged(nameof(CanConnect)); OnPropertyChanged(nameof(CanPair)); }
    }

    public string AndroidVersion
    {
        get => _androidVersion;
        set { _androidVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(VersionWarning)); }
    }

    public bool IsVersionChecked
    {
        get => _isVersionChecked;
        set { _isVersionChecked = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasVersionWarning)); }
    }

    public bool IsVersionSupported => _apiLevel >= 30;
    public bool HasVersionWarning => _isVersionChecked && !IsVersionSupported;

    public string VersionWarning => string.Format(LocalizationService.Translate("Wifi.VersionWarningMsg"), _androidVersion, _apiLevel);

    public ICommand RefreshCommand { get; }
    public ICommand GetIpCommand { get; }
    public ICommand EnableTcpIpCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand PairCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand FullAutoCommand { get; }

    private async Task CheckDeviceAsync()
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

        if (!string.IsNullOrEmpty(cur))
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];

        if (SelectedDevice != null)
        {
            var api = await _wifi.GetApiLevelAsync(SelectedDevice.Serial);
            var ver = await _wifi.GetAndroidVersionAsync(SelectedDevice.Serial);
            ApiLevel = api;
            AndroidVersion = ver;
            IsVersionChecked = true;

            AppendLog($"Device: {SelectedDevice.DisplayName}");
            AppendLog($"Android {ver} (API {api})");

            if (!IsVersionSupported)
            {
                StatusMessage = $"⚠ نیازمند Android 11+ - دستگاه شما: Android {ver} (API {api})";
                AppendLog("⚠ Version not supported for wireless ADB in this app.");
                AppendLog("  Requires Android 11+ (API 30+).");
            }
            else
            {
                StatusMessage = $"✓ Ready - {SelectedDevice.DisplayName}";
            }
        }
        else
        {
            IsVersionChecked = false;
            StatusMessage = "Connect a device via USB first.";
        }
    }

    private void AppendLog(string line)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogText += $"[{ts}] {line}\n";
        if (LogText.Length > 20000) LogText = LogText.Substring(LogText.Length - 15000);
    }

    private async Task GetIpAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }

        IsBusy = true;
        try
        {
            StatusMessage = "Getting device IP...";
            var ip = await _wifi.GetDeviceIpAsync(SelectedDevice.Serial);
            if (string.IsNullOrEmpty(ip))
            {
                StatusMessage = "✗ Could not detect WiFi IP. Is WiFi enabled?";
                AppendLog("✗ Failed to get IP - WiFi might be off");
                return;
            }
            DeviceIp = ip;
            StatusMessage = $"✓ Device IP: {ip}";
            AppendLog($"IP detected: {ip}");
        }
        finally { IsBusy = false; }
    }

    private async Task EnableTcpIpAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        if (!IsVersionSupported) { MessageBox.Show(VersionWarning, "Android 11+", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        IsBusy = true;
        try
        {
            StatusMessage = $"Enabling TCP/IP mode on port {TcpPort}...";
            AppendLog($"$ adb -s {SelectedDevice.Serial} tcpip {TcpPort}");

            var (ok, msg) = await _wifi.EnableTcpIpModeAsync(SelectedDevice.Serial, TcpPort);
            if (ok)
            {
                StatusMessage = $"✓ TCP/IP mode enabled on port {TcpPort}";
                AppendLog("✓ " + msg);
            }
            else
            {
                StatusMessage = $"✗ Failed: {msg}";
                AppendLog("✗ " + msg);
            }
        }
        finally { IsBusy = false; }
    }

    private async Task ConnectAsync()
    {
        if (!CanConnect) { StatusMessage = "Enter IP and port first."; return; }
        if (!IsVersionSupported) { MessageBox.Show(VersionWarning, "Android 11+", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        IsBusy = true;
        try
        {
            StatusMessage = $"Connecting to {DeviceIp}:{TcpPort}...";
            AppendLog($"$ adb connect {DeviceIp}:{TcpPort}");

            var (ok, msg) = await _wifi.ConnectAsync(DeviceIp, TcpPort);
            if (ok)
            {
                StatusMessage = $"✓ Connected to {DeviceIp}:{TcpPort}";
                AppendLog("✓ " + msg);
                var entry = $"{DeviceIp}:{TcpPort}";
                if (!PairedDevices.Contains(entry)) PairedDevices.Insert(0, entry);
            }
            else
            {
                StatusMessage = $"✗ Connect failed: {msg}";
                AppendLog("✗ " + msg);
            }
        }
        finally { IsBusy = false; }
    }

    private async Task DisconnectAsync()
    {
        if (string.IsNullOrWhiteSpace(DeviceIp)) { StatusMessage = "No IP."; return; }

        IsBusy = true;
        try
        {
            StatusMessage = $"Disconnecting {DeviceIp}:{TcpPort}...";
            AppendLog($"$ adb disconnect {DeviceIp}:{TcpPort}");
            var (ok, msg) = await _wifi.DisconnectAsync(DeviceIp, TcpPort);
            StatusMessage = ok ? "✓ Disconnected" : $"✗ {msg}";
            AppendLog((ok ? "✓ " : "✗ ") + msg);
        }
        finally { IsBusy = false; }
    }

    private async Task PairAsync()
    {
        if (!CanPair) { StatusMessage = "Enter IP, pairing port and 6-digit code."; return; }
        if (!IsVersionSupported) { MessageBox.Show(VersionWarning, "Android 11+", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        IsBusy = true;
        try
        {
            StatusMessage = $"Pairing with {DeviceIp}:{PairPort}...";
            AppendLog($"$ adb pair {DeviceIp}:{PairPort} [code]");

            var (ok, msg) = await _wifi.PairAsync(DeviceIp, PairPort, PairCode);
            StatusMessage = ok ? "✓ Paired successfully" : $"✗ Pair failed: {msg}";
            AppendLog((ok ? "✓ " : "✗ ") + msg);

            if (ok)
            {
                // Try standard connect on default port
                await Task.Delay(500);
                AppendLog("Trying to connect on default port 5555...");
                var (cok, cmsg) = await _wifi.ConnectAsync(DeviceIp, 5555);
                AppendLog((cok ? "✓ " : "✗ ") + cmsg);
            }
        }
        finally { IsBusy = false; }
    }

    private async Task FullAutoAsync()
    {
        // Convenience: get IP → enable tcpip → connect
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        if (!IsVersionSupported) { MessageBox.Show(VersionWarning, "Android 11+", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        await GetIpAsync();
        if (string.IsNullOrWhiteSpace(DeviceIp)) return;

        await EnableTcpIpAsync();
        await Task.Delay(1000);
        await ConnectAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
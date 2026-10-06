// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

/// <summary>
/// ViewModel for the Live Monitor page - polls every second and
/// pushes values to rolling history collections for the sparklines.
/// </summary>
public class MonitorViewModel : INotifyPropertyChanged
{
    private const int HistoryLength = 60; // 60 seconds window

    private readonly AdbService _adb;
    private readonly MonitorService _monitor;
    private readonly ActivityLogService _activity;

    private DispatcherTimer? _timer;
    private CancellationTokenSource? _cts;
    private bool _isPolling;
    private bool _isRunning;

    private DeviceModel? _selectedDevice;
    private MonitorSample _current = new();
    private string _statusMessage = "Press Start to begin monitoring.";

    public MonitorViewModel(AdbService adb, MonitorService monitor, ActivityLogService activity)
    {
        _adb = adb;
        _monitor = monitor;
        _activity = activity;

        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
        StartCommand = new RelayCommand(_ => Start());
        StopCommand = new RelayCommand(_ => Stop());
        ClearCommand = new RelayCommand(_ => ClearHistory());
    }

    // ─── History buffers for charts ───
    public ObservableCollection<double> CpuHistory { get; } = new();
    public ObservableCollection<double> RamHistory { get; } = new();
    public ObservableCollection<double> NetUpHistory { get; } = new();
    public ObservableCollection<double> NetDownHistory { get; } = new();

    public ObservableCollection<DeviceModel> Devices { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged();
            if (value != null) _monitor.Reset();
        }
    }

    public MonitorSample Current
    {
        get => _current;
        set
        {
            _current = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CpuDisplay));
            OnPropertyChanged(nameof(RamDisplay));
            OnPropertyChanged(nameof(RamDetail));
            OnPropertyChanged(nameof(NetUpDisplay));
            OnPropertyChanged(nameof(NetDownDisplay));
            OnPropertyChanged(nameof(LoadDisplay));
        }
    }

    public string CpuDisplay => Current.CpuDisplay;
    public string RamDisplay => Current.RamDisplay;
    public string RamDetail => Current.RamDetail;
    public string NetUpDisplay => Current.NetUpDisplay;
    public string NetDownDisplay => Current.NetDownDisplay;
    public string LoadDisplay => Current.LoadDisplay;

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); }
    }

    public ICommand RefreshDevicesCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearCommand { get; }

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
    }

    public async Task RefreshDevicesAsync()
    {
        if (!_adb.IsAdbAvailable)
        {
            StatusMessage = "adb not found.";
            return;
        }

        try
        {
            var list = await _adb.GetDevicesAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Devices.Clear();
                foreach (var d in list.Where(d => d.IsReady)) Devices.Add(d);
                if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];
            });
        }
        catch (Exception ex) { StatusMessage = $"Failed to scan: {ex.Message}"; }
    }

    private void Start()
    {
        if (SelectedDevice == null) { StatusMessage = "Select a device first."; return; }
        if (IsRunning) return;

        _monitor.Reset();
        ClearHistory();

        _cts = new CancellationTokenSource();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();

        IsRunning = true;
        StatusMessage = $"Monitoring {SelectedDevice.DisplayName}...";
        _activity.LogInfo($"Monitor started: {SelectedDevice.DisplayName}", "🎬", "#22D3EE");
    }

    private void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _cts?.Cancel();
        _cts = null;
        IsRunning = false;
        StatusMessage = "Monitoring stopped.";
    }

    private void ClearHistory()
    {
        CpuHistory.Clear();
        RamHistory.Clear();
        NetUpHistory.Clear();
        NetDownHistory.Clear();
    }

    private async Task TickAsync()
    {
        if (_isPolling || SelectedDevice == null || _cts == null) return;
        _isPolling = true;
        try
        {
            var sample = await _monitor.SampleAsync(SelectedDevice.Serial, _cts.Token);
            Current = sample;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Push(CpuHistory, sample.CpuPercent);
                Push(RamHistory, sample.RamPercent);
                Push(NetUpHistory, sample.NetUpKbps);
                Push(NetDownHistory, sample.NetDownKbps);
            });
        }
        catch (OperationCanceledException) { /* stopped */ }
        catch (Exception ex) { StatusMessage = $"Sample error: {ex.Message}"; }
        finally { _isPolling = false; }
    }

    private static void Push(ObservableCollection<double> col, double value)
    {
        col.Add(value);
        while (col.Count > HistoryLength) col.RemoveAt(0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
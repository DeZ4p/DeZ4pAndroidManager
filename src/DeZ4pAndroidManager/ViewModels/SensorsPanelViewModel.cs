// © DeZ4p | t.me/DeZ4p | All Rights Reserved

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
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class SensorsPanelViewModel : INotifyPropertyChanged
{
    private readonly SensorService _svc;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private string _searchText = "";
    private bool _isLoading;
    private string _statusMessage = "Ready.";
    private string _rawDump = "";
    private string _diagnostic = "";
    private string _lastStrategy = "";
    private SensorInfo? _selectedSensor;

    private System.Collections.Generic.List<SensorInfo> _allSensors = new();

    public SensorsPanelViewModel(SensorService svc, DeviceStateService deviceState,
                                  DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LoadRawCommand = new AsyncRelayCommand(LoadRawAsync);
        SaveRawCommand = new RelayCommand(_ => SaveRaw());
        CopyRawCommand = new RelayCommand(_ => CopyRaw());
        CopySensorCommand = new RelayCommand(_ => CopySensor());
        CopyDiagnosticCommand = new RelayCommand(_ => CopyDiagnostic());

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());

        _ = RefreshAsync();
    }

    public ObservableCollection<SensorInfo> Sensors { get; } = new();
    public ObservableCollection<DeviceModel> Devices { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }

    public bool HasDevice => _selectedDevice != null;

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; OnPropertyChanged(); ApplyFilter(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public string RawDump
    {
        get => _rawDump;
        set { _rawDump = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasRawDump)); }
    }

    public bool HasRawDump => !string.IsNullOrEmpty(_rawDump);

    public string Diagnostic
    {
        get => _diagnostic;
        set { _diagnostic = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiagnostic)); }
    }

    public bool HasDiagnostic => !string.IsNullOrEmpty(_diagnostic);

    public string LastStrategy
    {
        get => _lastStrategy;
        set { _lastStrategy = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(StrategyDisplay)); }
    }

    public string StrategyDisplay => string.IsNullOrEmpty(LastStrategy)
        ? ""
        : $"Strategy: {LastStrategy}";

    public SensorInfo? SelectedSensor
    {
        get => _selectedSensor;
        set { _selectedSensor = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSensorDetails)); }
    }

    public bool HasSensorDetails => _selectedSensor != null;

    public ICommand RefreshCommand { get; }
    public ICommand LoadRawCommand { get; }
    public ICommand SaveRawCommand { get; }
    public ICommand CopyRawCommand { get; }
    public ICommand CopySensorCommand { get; }
    public ICommand CopyDiagnosticCommand { get; }

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

        if (!string.IsNullOrEmpty(cur))
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];

        if (SelectedDevice == null)
        {
            Sensors.Clear();
            _allSensors.Clear();
            StatusMessage = "No device connected.";
            return;
        }

        IsLoading = true;
        try
        {
            _allSensors = await _svc.ListAsync(SelectedDevice.Serial);
            ApplyFilter();

            LastStrategy = _svc.LastStrategy;
            Diagnostic = _svc.LastDiagnostic;
            // Auto-populate raw so user can diagnose if empty
            if (string.IsNullOrEmpty(RawDump))
                RawDump = _svc.LastRawDump;

            if (_allSensors.Count == 0)
            {
                StatusMessage = "⚠ No sensors parsed. Check the Raw dump below.";
            }
            else
            {
                StatusMessage = $"Found {_allSensors.Count} sensor(s)";
            }
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void ApplyFilter()
    {
        var q = _allSensors.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(x =>
                (x.Name?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (x.Vendor?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (x.TypeName?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));
        }
        Sensors.Clear();
        foreach (var s in q) Sensors.Add(s);
    }

    private async Task LoadRawAsync()
    {
        if (SelectedDevice == null) return;
        IsLoading = true;
        try
        {
            RawDump = await _svc.GetRawDumpAsync(SelectedDevice.Serial);
            StatusMessage = $"Raw dump loaded ({RawDump.Length} chars)";
        }
        finally { IsLoading = false; }
    }

    /// <summary>Auto-save to Documents\DeZ4p Android Manager\Sensors Dumps\</summary>
    private void SaveRaw()
    {
        if (string.IsNullOrEmpty(RawDump)) return;
        try
        {
            var folder = AppPaths.SensorsDumps;
            Directory.CreateDirectory(folder);
            var fname = $"sensors_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            var path = Path.Combine(folder, fname);

            var content = RawDump + "\n\n=== DIAGNOSTIC ===\n" + Diagnostic;
            File.WriteAllText(path, content, Encoding.UTF8);
            StatusMessage = $"✓ Saved: Sensors Dumps\\{fname}";
        }
        catch (Exception ex) { StatusMessage = $"✗ {ex.Message}"; }
    }

    private void CopyRaw()
    {
        if (string.IsNullOrEmpty(RawDump)) return;
        try { Clipboard.SetText(RawDump); StatusMessage = "Raw dump copied."; }
        catch { }
    }

    private void CopyDiagnostic()
    {
        if (string.IsNullOrEmpty(Diagnostic)) return;
        try { Clipboard.SetText(Diagnostic); StatusMessage = "Diagnostic copied."; }
        catch { }
    }

    private void CopySensor()
    {
        var s = _selectedSensor;
        if (s == null) return;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Name:        {s.Name}");
            sb.AppendLine($"Vendor:      {s.Vendor}");
            sb.AppendLine($"Version:     {s.Version}");
            sb.AppendLine($"Type:        {s.TypeName} ({s.TypeId})");
            sb.AppendLine($"Handle:      0x{s.Handle:X8}");
            if (!string.IsNullOrEmpty(s.MaxRange)) sb.AppendLine($"Max range:   {s.MaxRange}");
            if (!string.IsNullOrEmpty(s.Resolution)) sb.AppendLine($"Resolution:  {s.Resolution}");
            if (!string.IsNullOrEmpty(s.Power)) sb.AppendLine($"Power:       {s.Power} mA");
            if (!string.IsNullOrEmpty(s.MinDelay)) sb.AppendLine($"Min delay:   {s.MinDelay} µs");
            if (!string.IsNullOrEmpty(s.FifoMax)) sb.AppendLine($"FIFO max:    {s.FifoMax}");
            sb.AppendLine();
            sb.AppendLine("Raw line:");
            sb.AppendLine(s.RawLine);
            Clipboard.SetText(sb.ToString());
            StatusMessage = "Sensor info copied.";
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
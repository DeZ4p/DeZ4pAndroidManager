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
using System.Windows.Media;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class SecurityViewModel : INotifyPropertyChanged
{
    private readonly SecurityService _svc;
    private readonly DeviceStateService _deviceState;
    private DeviceModel? _selectedDevice;
    private bool _isLoading;
    private string _statusMessage = "Ready.";
    private SecurityReport? _report;

    public SecurityViewModel(SecurityService svc, DeviceStateService deviceState, DeviceWatcherService watcher)
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
    public DeviceModel? SelectedDevice { get => _selectedDevice; set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); } }
    public bool HasDevice => _selectedDevice != null;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }

    public SecurityReport? Report
    {
        get => _report;
        set { _report = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasReport)); OnPropertyChanged(nameof(ScoreDisplay)); OnPropertyChanged(nameof(GradeDisplay)); OnPropertyChanged(nameof(ScoreColor)); }
    }
    public bool HasReport => _report != null;
    public string ScoreDisplay => _report == null || _report.Score < 0 ? "--" : _report.Score.ToString();
    public string GradeDisplay => _report?.Grade ?? "N/A";
    public Brush ScoreColor
    {
        get
        {
            var hex = _report?.Score switch
            {
                >= 90 => "#34D399",
                >= 75 => "#22D3EE",
                >= 60 => "#F59E0B",
                >= 40 => "#F97316",
                >= 0  => "#EF4444",
                _     => "#6B7280"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

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
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        IsLoading = true;
        try
        {
            StatusMessage = "Analyzing security...";
            Report = await _svc.GetAsync(SelectedDevice.Serial);
            StatusMessage = $"Security score: {Report.Score}/100 ({Report.Grade})";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void SaveReport()
    {
        if (_report == null) return;
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"security_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Security Report");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine($"Score: {_report.Score}/100 ({_report.Grade})");
            sb.AppendLine();
            foreach (var c in _report.Checks)
                sb.AppendLine($"[{c.LevelLabel,-8}] {c.Title,-22} {c.Value,-22} {c.Hint}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"✓ Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"✗ {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
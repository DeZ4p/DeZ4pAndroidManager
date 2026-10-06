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

namespace DeZ4pAndroidManager.ViewModels;

public class PrivacyViewModel : INotifyPropertyChanged
{
    private readonly PrivacyService _svc;
    private readonly DeviceStateService _deviceState;
    private DeviceModel? _selectedDevice;
    private bool _isLoading;
    private string _statusMessage = "Ready.";

    public PrivacyViewModel(PrivacyService svc, DeviceStateService deviceState, DeviceWatcherService watcher)
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
    public ObservableCollection<PrivacyCheck> Checks { get; } = new();
    public DeviceModel? SelectedDevice { get => _selectedDevice; set { _selectedDevice = value; OnPropertyChanged(); } }
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }
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
            if (w != null) foreach (var d in w.CurrentDevices.Where(x => x.IsReady && !x.IsFastboot)) Devices.Add(d);
        }
        catch { }
        if (!string.IsNullOrEmpty(cur)) SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        IsLoading = true;
        try
        {
            StatusMessage = "Analyzing privacy...";
            var list = await _svc.GetChecksAsync(SelectedDevice.Serial);
            Checks.Clear();
            foreach (var c in list) Checks.Add(c);
            StatusMessage = $"Loaded {Checks.Count} checks";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void SaveReport()
    {
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"privacy_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Privacy Report");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            foreach (var c in Checks) sb.AppendLine($"[{c.Level.ToUpper(),-4}] {c.Title,-28} {c.Value,-15} {c.Hint}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
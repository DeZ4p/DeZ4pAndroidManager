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

public class PermissionsViewModel : INotifyPropertyChanged
{
    private readonly PermissionsService _svc;
    private readonly DeviceStateService _deviceState;
    private DeviceModel? _selectedDevice;
    private PermissionApp? _selectedApp;
    private bool _isLoading;
    private string _statusMessage = "Ready.";

    public PermissionsViewModel(PermissionsService svc, DeviceStateService deviceState, DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        RevokeCommand = new AsyncRelayCommand<string>(RevokeAsync);
        GrantCommand = new AsyncRelayCommand<string>(GrantAsync);
        SaveReportCommand = new RelayCommand(_ => SaveReport());
        OpenFolderCommand = new RelayCommand(_ => AppPaths.OpenFolder(Path.Combine(AppPaths.Root, "Reports")));
        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _ = RefreshAsync();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<PermissionApp> Apps { get; } = new();

    public DeviceModel? SelectedDevice { get => _selectedDevice; set { _selectedDevice = value; OnPropertyChanged(); } }
    public PermissionApp? SelectedApp { get => _selectedApp; set { _selectedApp = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasApp)); } }
    public bool HasApp => _selectedApp != null;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }

    public ICommand RefreshCommand { get; }
    public ICommand RevokeCommand { get; }
    public ICommand GrantCommand { get; }
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
            StatusMessage = "Scanning permissions...";
            var list = await _svc.GetAppsWithPermissionsAsync(SelectedDevice.Serial);
            Apps.Clear();
            foreach (var a in list) Apps.Add(a);
            StatusMessage = $"{Apps.Count} app(s) with dangerous permissions";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private async Task RevokeAsync(string? perm)
    {
        if (_selectedApp == null || string.IsNullOrEmpty(perm)) return;
        var (ok, msg) = await _svc.RevokeAsync(SelectedDevice!.Serial, _selectedApp.PackageName, perm);
        StatusMessage = ok ? $"Revoked: {perm}" : $"Failed: {msg}";
        if (ok) await RefreshAsync();
    }

    private async Task GrantAsync(string? perm)
    {
        if (_selectedApp == null || string.IsNullOrEmpty(perm)) return;
        var (ok, msg) = await _svc.GrantAsync(SelectedDevice!.Serial, _selectedApp.PackageName, perm);
        StatusMessage = ok ? $"Granted: {perm}" : $"Failed: {msg}";
        if (ok) await RefreshAsync();
    }

    private void SaveReport()
    {
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"permissions_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Permissions Report");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine();
            foreach (var a in Apps)
            {
                sb.AppendLine($"── {a.AppName} ({a.PackageName}) ──");
                foreach (var p in a.GrantedPermissions) sb.AppendLine($"  [GRANTED] {p}");
                foreach (var p in a.DeniedPermissions) sb.AppendLine($"  [DENIED]  {p}");
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
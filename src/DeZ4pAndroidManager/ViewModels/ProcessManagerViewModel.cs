// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
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

public class ProcessManagerViewModel : INotifyPropertyChanged
{
    private readonly ProcessService _svc;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private string _searchText = "";
    private string _filterUser = "All";
    private bool _isLoading;
    private bool _isBusy;
    private bool _showSystemProcesses = true;
    private string _statusMessage = "Ready.";
    private ProcessInfo? _selectedProcess;

    private List<ProcessInfo> _allProcesses = new();

    public ProcessManagerViewModel(ProcessService svc, DeviceStateService deviceState,
                                    DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        KillCommand = new AsyncRelayCommand(KillAsync);
        ForceStopCommand = new AsyncRelayCommand(ForceStopAsync);
        CopyInfoCommand = new RelayCommand(_ => CopyInfo());

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());

        _ = RefreshAsync();
    }

    public ObservableCollection<ProcessInfo> Processes { get; } = new();
    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public List<string> UserFilterOptions { get; } = new() { "All", "User apps", "System", "root", "system", "shell" };

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

    public string FilterUser
    {
        get => _filterUser;
        set { _filterUser = value ?? "All"; OnPropertyChanged(); ApplyFilter(); }
    }

    public bool ShowSystemProcesses
    {
        get => _showSystemProcesses;
        set { _showSystemProcesses = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set { _selectedProcess = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedProcess != null;

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

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

    public ICommand RefreshCommand { get; }
    public ICommand KillCommand { get; }
    public ICommand ForceStopCommand { get; }
    public ICommand CopyInfoCommand { get; }

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
            Processes.Clear();
            StatusMessage = "No device connected.";
            return;
        }

        IsLoading = true;
        try
        {
            _allProcesses = await _svc.ListAsync(SelectedDevice.Serial);
            ApplyFilter();
            StatusMessage = $"Loaded {_allProcesses.Count} processes";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void ApplyFilter()
    {
        var query = _allProcesses.AsEnumerable();

        if (!ShowSystemProcesses)
            query = query.Where(p => p.IsUserApp);

        var f = FilterUser ?? "All";
        if (f == "User apps") query = query.Where(p => p.IsUserApp);
        else if (f == "System") query = query.Where(p => !p.IsUserApp);
        else if (f == "root") query = query.Where(p => p.User == "root");
        else if (f == "system") query = query.Where(p => p.User == "system");
        else if (f == "shell") query = query.Where(p => p.User == "shell");

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(p =>
                (p.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (p.User?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                p.Pid.ToString().Contains(q));
        }

        Processes.Clear();
        foreach (var p in query) Processes.Add(p);
    }

    private async Task KillAsync()
    {
        var p = _selectedProcess;
        if (p == null || SelectedDevice == null) return;

        var r = MessageBox.Show(
            $"Kill process?\n\nPID: {p.Pid}\nUser: {p.User}\nName: {p.Name}\n\n" +
            "This may require root. System processes will not be killed.",
            "Confirm Kill",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            StatusMessage = $"Killing PID {p.Pid}...";
            var (ok, msg) = await _svc.KillAsync(SelectedDevice.Serial, p.Pid);
            StatusMessage = ok ? $"✓ {msg}" : $"✗ {msg}";
            if (ok) await Task.Delay(500);
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task ForceStopAsync()
    {
        var p = _selectedProcess;
        if (p == null || SelectedDevice == null) return;

        // Extract package name
        var pkg = p.Name;
        if (pkg.Contains(':')) pkg = pkg.Split(':')[0];
        if (pkg.Contains('/')) pkg = pkg.Split('/')[0];

        var r = MessageBox.Show(
            $"Force-stop package?\n\nPackage: {pkg}\n\n" +
            "This sends a graceful stop signal to the app.",
            "Confirm Force-Stop",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            StatusMessage = $"Force-stopping {pkg}...";
            var (ok, msg) = await _svc.ForceStopAsync(SelectedDevice.Serial, pkg);
            StatusMessage = ok ? $"✓ {msg}" : $"✗ {msg}";
            if (ok) await Task.Delay(700);
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    private void CopyInfo()
    {
        var p = _selectedProcess;
        if (p == null) return;
        try
        {
            var txt = $"PID: {p.Pid}\nPPID: {p.Ppid}\nUSER: {p.User}\nSTATE: {p.State}\n" +
                      $"RSS: {p.RssDisplay}\nVSZ: {p.VszDisplay}\nNAME: {p.Name}";
            Clipboard.SetText(txt);
            StatusMessage = "Copied process info.";
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
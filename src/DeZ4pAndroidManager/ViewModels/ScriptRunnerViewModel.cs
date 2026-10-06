// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class ScriptRunnerViewModel : INotifyPropertyChanged
{
    private readonly ScriptRunnerService _svc;
    private readonly DeviceStateService _deviceState;
    private DeviceModel? _selectedDevice;
    private ScriptItem? _selectedScript;
    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private double _progress;
    private CancellationTokenSource? _cts;

    public ScriptRunnerViewModel(ScriptRunnerService svc, DeviceStateService deviceState, DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;
        Scripts = new ObservableCollection<ScriptItem>(ScriptRunnerService.GetBuiltInScripts());
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());
        RunCommand = new AsyncRelayCommand(RunAsync);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel());
        ClearCommand = new RelayCommand(_ => Results.Clear());
        SaveReportCommand = new RelayCommand(_ => SaveReport());
        OpenFolderCommand = new RelayCommand(_ => AppPaths.OpenFolder(Path.Combine(AppPaths.Root, "Reports")));
        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        RefreshDevices();
    }

    public ObservableCollection<ScriptItem> Scripts { get; }
    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<ScriptResult> Results { get; } = new();
    public ObservableCollection<ScriptItem> FilteredScripts { get; } = new();

    public DeviceModel? SelectedDevice { get => _selectedDevice; set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); } }
    public bool HasDevice => _selectedDevice != null;

    public ScriptItem? SelectedScript { get => _selectedScript; set { _selectedScript = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasScript)); OnPropertyChanged(nameof(CommandCount)); } }
    public bool HasScript => _selectedScript != null;
    public string CommandCount => _selectedScript == null ? "" : $"{_selectedScript.Commands.Count} command(s)";

    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }

    public ICommand RefreshDevicesCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SaveReportCommand { get; }
    public ICommand OpenFolderCommand { get; }

    private void RefreshDevices()
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
        if (SelectedScript == null && Scripts.Count > 0) SelectedScript = Scripts[0];
    }

    private async Task RunAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "Select a device first."; return; }
        if (SelectedScript == null) { StatusMessage = "Select a script."; return; }
        Results.Clear();
        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
        try
        {
            var prog = new Progress<(int idx, int total, string cmd)>(p =>
            {
                Progress = p.total > 0 ? (double)p.idx / p.total * 100 : 0;
                StatusMessage = $"Running [{p.idx}/{p.total}]: {p.cmd}";
            });
            var list = await _svc.RunAsync(SelectedDevice.Serial, SelectedScript, prog, _cts.Token);
            foreach (var r in list) Results.Add(r);
            Progress = 100;
            StatusMessage = $"Done — {list.Count(r => r.Success)}/{list.Count} succeeded";
        }
        catch (OperationCanceledException) { StatusMessage = "Cancelled."; }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void SaveReport()
    {
        if (Results.Count == 0) return;
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"script_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"DeZ4p Script Report");
            sb.AppendLine($"Script: {SelectedScript?.Name}");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            foreach (var r in Results)
            {
                sb.AppendLine($"> {r.Command}");
                sb.AppendLine($"  exit={r.ExitCode} ({r.DurationMs}ms)");
                sb.AppendLine(r.Output);
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
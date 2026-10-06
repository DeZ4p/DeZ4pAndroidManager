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

public class TaskSchedulerViewModel : INotifyPropertyChanged
{
    private readonly TaskSchedulerService _svc;
    private readonly DeviceStateService _deviceState;
    private DeviceModel? _selectedDevice;
    private ScheduledTask? _selected;
    private string _newName = "";
    private string _newCommand = "";
    private int _newInterval = 60;
    private string _statusMessage = "Ready.";

    public TaskSchedulerViewModel(TaskSchedulerService svc, DeviceStateService deviceState, DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;
        svc.Load();
        foreach (var t in svc.Tasks) Tasks.Add(t);

        AddCommand = new RelayCommand(_ => AddTask());
        RemoveCommand = new RelayCommand(p => RemoveTask(p as ScheduledTask));
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());
        StartCommand = new RelayCommand(_ => Start());
        StopCommand = new RelayCommand(_ => Stop());
        SaveCommand = new RelayCommand(_ => _svc.Save());
        ToggleEnableCommand = new RelayCommand(p => { if (p is ScheduledTask t) t.IsEnabled = !t.IsEnabled; });

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        RefreshDevices();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<ScheduledTask> Tasks { get; } = new();
    public DeviceModel? SelectedDevice { get => _selectedDevice; set { _selectedDevice = value; OnPropertyChanged(); } }
    public ScheduledTask? Selected { get => _selected; set { _selected = value; OnPropertyChanged(); } }
    public string NewName { get => _newName; set { _newName = value ?? ""; OnPropertyChanged(); } }
    public string NewCommand { get => _newCommand; set { _newCommand = value ?? ""; OnPropertyChanged(); } }
    public int NewInterval { get => _newInterval; set { _newInterval = Math.Max(5, value); OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }

    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleEnableCommand { get; }

    private void RefreshDevices()
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
    }

    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewCommand)) return;
        var t = new ScheduledTask { Name = NewName, Command = NewCommand, IntervalSeconds = NewInterval };
        Tasks.Add(t);
        _svc.Tasks.Add(t);
        _svc.Save();
        NewName = ""; NewCommand = "";
        StatusMessage = $"Task added: {t.Name}";
    }

    private void RemoveTask(ScheduledTask? t)
    {
        if (t == null) return;
        Tasks.Remove(t);
        _svc.Tasks.Remove(t);
        _svc.Save();
    }

    private void Start()
    {
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        _svc.Start(SelectedDevice.Serial);
        StatusMessage = $"Scheduler running on {SelectedDevice.DisplayName}";
    }

    private void Stop()
    {
        _svc.Stop();
        StatusMessage = "Scheduler stopped.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
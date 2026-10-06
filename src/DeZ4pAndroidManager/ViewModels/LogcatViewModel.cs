// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class LogcatViewModel : INotifyPropertyChanged
{
    private readonly LogcatService _logcat;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private bool _isRunning;
    private bool _isPaused;
    private bool _autoScroll = true;
    private string _statusMessage = "Ready.";
    private string _searchText = "";
    private string _selectedLevel = "V";
    private string _selectedPreset = "All";
    private int _maxLines = 5000;
    private int _totalReceived = 0;
    private int _shownCount = 0;

    private readonly ConcurrentQueue<LogEntry> _pending = new();
    private readonly DispatcherTimer _flushTimer;

    public LogcatViewModel(LogcatService logcat, DeviceStateService deviceState,
                            DeviceWatcherService watcher)
    {
        _logcat = logcat;
        _deviceState = deviceState;

        AllEntries = new ObservableCollection<LogEntry>();
        FilteredEntries = new ObservableCollection<LogEntry>();

        _logcat.LineReceived += (_, line) => OnLineReceived(line);
        _logcat.ProcessExited += (_, _) => Application.Current?.Dispatcher.Invoke(() =>
        {
            IsRunning = false;
            StatusMessage = "logcat stopped.";
        });

        StartCommand = new AsyncRelayCommand(StartAsync);
        StopCommand = new AsyncRelayCommand(StopAsync);
        PauseCommand = new RelayCommand(_ => IsPaused = !IsPaused);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync);
        CopyCommand = new RelayCommand(_ => CopyAll());
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());
        ApplyFilterCommand = new RelayCommand(_ => ApplyFilter());
        OpenLogsFolderCommand = new RelayCommand(_ => AppPaths.OpenFolder(AppPaths.Logs));

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);

        _flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _flushTimer.Tick += (_, _) => FlushPending();
        _flushTimer.Start();

        RefreshDevices();
    }

    public ObservableCollection<LogEntry> AllEntries { get; }
    public ObservableCollection<LogEntry> FilteredEntries { get; }
    public ObservableCollection<DeviceModel> Devices { get; } = new();

    public List<string> LevelOptions { get; } = new() { "V", "D", "I", "W", "E", "F" };
    public List<string> PresetOptions { get; } = new()
    {
        "All",
        "Errors only",
        "Warnings + Errors",
        "Info and above",
        "Debug and above",
        "ActivityManager",
        "PackageManager",
        "SystemServer",
        "WindowManager",
        "NetworkStats",
        "ConnectivityService",
        "AndroidRuntime (crashes)"
    };

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }

    public bool HasDevice => _selectedDevice != null;

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set { _isPaused = value; OnPropertyChanged(); }
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set { _autoScroll = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; OnPropertyChanged(); }
    }

    public string SelectedLevel
    {
        get => _selectedLevel;
        set { _selectedLevel = value ?? "V"; OnPropertyChanged(); }
    }

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value ?? "";
            OnPropertyChanged();
            ApplyPresetFilter();
        }
    }

    public int MaxLines
    {
        get => _maxLines;
        set { _maxLines = Math.Max(500, Math.Min(50000, value)); OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public int TotalReceived
    {
        get => _totalReceived;
        set { _totalReceived = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountDisplay)); }
    }

    public int ShownCount
    {
        get => _shownCount;
        set { _shownCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountDisplay)); }
    }

    public string CountDisplay => $"{ShownCount} / {TotalReceived}";

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }

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

        if (!string.IsNullOrEmpty(cur))
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];
    }

    private void OnLineReceived(string line)
    {
        if (IsPaused) return;
        var entry = ParseLine(line);
        if (entry == null) return;
        _pending.Enqueue(entry);
    }

    private static readonly Regex LogcatRegex = new(
        @"^(?<mm>\d{2})-(?<dd>\d{2})\s+(?<hh>\d{2}):(?<mi>\d{2}):(?<ss>\d{2})\.(?<ms>\d{3})\s+(?<pid>\d+)\s+(?<tid>\d+)\s+(?<lvl>[VDIWEFA])\s+(?<tag>[^:]+):\s*(?<msg>.*)$",
        RegexOptions.Compiled);

    private LogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m = LogcatRegex.Match(line);
        if (m.Success)
        {
            var entry = new LogEntry
            {
                Raw = line,
                Level = m.Groups["lvl"].Value,
                Tag = m.Groups["tag"].Value.Trim(),
                Message = m.Groups["msg"].Value,
                Pid = int.TryParse(m.Groups["pid"].Value, out var pid) ? pid : 0,
                Tid = int.TryParse(m.Groups["tid"].Value, out var tid) ? tid : 0
            };
            try
            {
                entry.Time = new DateTime(
                    DateTime.Now.Year,
                    int.Parse(m.Groups["mm"].Value),
                    int.Parse(m.Groups["dd"].Value),
                    int.Parse(m.Groups["hh"].Value),
                    int.Parse(m.Groups["mi"].Value),
                    int.Parse(m.Groups["ss"].Value),
                    int.Parse(m.Groups["ms"].Value));
            }
            catch { }
            return entry;
        }

        return new LogEntry
        {
            Raw = line,
            Level = "I",
            Tag = "logcat",
            Message = line
        };
    }

    private void FlushPending()
    {
        if (_pending.IsEmpty) return;

        var drained = 0;
        var newEntries = new List<LogEntry>(200);
        while (drained < 500 && _pending.TryDequeue(out var entry))
        {
            newEntries.Add(entry);
            drained++;
        }

        if (newEntries.Count == 0) return;

        TotalReceived += newEntries.Count;

        int overflow = AllEntries.Count + newEntries.Count - MaxLines;
        if (overflow > 0)
        {
            for (int i = 0; i < overflow && AllEntries.Count > 0; i++)
                AllEntries.RemoveAt(0);
        }

        foreach (var e in newEntries)
            AllEntries.Add(e);

        bool noFilter = string.IsNullOrEmpty(SearchText)
                     && SelectedLevel == "V"
                     && (SelectedPreset == "All" || string.IsNullOrEmpty(SelectedPreset));

        if (noFilter)
        {
            foreach (var e in newEntries)
                FilteredEntries.Add(e);
            while (FilteredEntries.Count > MaxLines) FilteredEntries.RemoveAt(0);
            ShownCount = FilteredEntries.Count;
        }
        else
        {
            foreach (var e in newEntries)
                if (MatchesFilter(e))
                    FilteredEntries.Add(e);
            while (FilteredEntries.Count > MaxLines) FilteredEntries.RemoveAt(0);
            ShownCount = FilteredEntries.Count;
        }
    }

    private bool MatchesFilter(LogEntry e)
    {
        if (e == null) return false;

        var levelRank = LevelRank(e.Level ?? "V");
        if (levelRank < LevelRank(SelectedLevel ?? "V")) return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            var tagHit = !string.IsNullOrEmpty(e.Tag) &&
                         e.Tag.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            var msgHit = !string.IsNullOrEmpty(e.Message) &&
                         e.Message.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!tagHit && !msgHit) return false;
        }

        var preset = SelectedPreset ?? "";
        var tag = e.Tag ?? "";

        switch (preset)
        {
            case "ActivityManager":
                if (!string.Equals(tag, "ActivityManager", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case "PackageManager":
                if (!string.Equals(tag, "PackageManager", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case "SystemServer":
                if (!string.Equals(tag, "SystemServer", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case "WindowManager":
                if (!string.Equals(tag, "WindowManager", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case "NetworkStats":
                if (!string.Equals(tag, "NetworkStats", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case "ConnectivityService":
                if (!string.Equals(tag, "ConnectivityService", StringComparison.OrdinalIgnoreCase)) return false;
                break;
            case "AndroidRuntime (crashes)":
                if (!string.Equals(tag, "AndroidRuntime", StringComparison.OrdinalIgnoreCase)) return false;
                break;
        }

        return true;
    }

    private static int LevelRank(string? lvl)
    {
        return (lvl ?? "V") switch
        {
            "V" => 0, "D" => 1, "I" => 2, "W" => 3, "E" => 4, "F" => 5, "A" => 6, _ => 0
        };
    }

    private void ApplyPresetFilter()
    {
        var p = SelectedPreset ?? "All";
        switch (p)
        {
            case "Errors only": SelectedLevel = "E"; SearchText = ""; break;
            case "Warnings + Errors": SelectedLevel = "W"; SearchText = ""; break;
            case "Info and above": SelectedLevel = "I"; SearchText = ""; break;
            case "Debug and above": SelectedLevel = "D"; SearchText = ""; break;
            case "All": SelectedLevel = "V"; SearchText = ""; break;
            default: SelectedLevel = "V"; SearchText = ""; break;
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        foreach (var e in AllEntries)
            if (MatchesFilter(e))
                FilteredEntries.Add(e);
        ShownCount = FilteredEntries.Count;
        StatusMessage = $"Filtered: {FilteredEntries.Count} / {AllEntries.Count}";
    }

    private async Task StartAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "Select a device first."; return; }
        if (IsRunning) return;

        AllEntries.Clear();
        FilteredEntries.Clear();
        TotalReceived = 0;
        ShownCount = 0;

        StatusMessage = "Starting logcat...";
        var (ok, err) = await _logcat.StartAsync(
            SelectedDevice.Serial,
            string.IsNullOrEmpty(SelectedPreset) || SelectedPreset == "All" ? "V" : SelectedLevel,
            "");

        if (!ok) { StatusMessage = $"✗ {err}"; return; }

        IsRunning = true;
        StatusMessage = "● Streaming logcat...";
    }

    private async Task StopAsync()
    {
        await _logcat.StopAsync();
        IsRunning = false;
        StatusMessage = "Stopped.";
    }

    private async Task ClearAsync()
    {
        AllEntries.Clear();
        FilteredEntries.Clear();
        TotalReceived = 0;
        ShownCount = 0;
        if (SelectedDevice != null)
            await _logcat.ClearAsync(SelectedDevice.Serial);
        StatusMessage = "Cleared.";
    }

    /// <summary>⭐ Auto-save to Documents\DeZ4p Android Manager\Logs\ - no dialog.</summary>
    private async Task SaveAsync()
    {
        try
        {
            var folder = AppPaths.Logs;
            Directory.CreateDirectory(folder);
            var fname = $"logcat_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            var path = Path.Combine(folder, fname);

            var sb = new StringBuilder(AllEntries.Count * 100);
            foreach (var e in AllEntries)
                sb.AppendLine(e.Raw);

            await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"✓ Saved: Logs\\{fname}";
        }
        catch (Exception ex) { StatusMessage = $"✗ {ex.Message}"; }
    }

    /// <summary>Save with dialog (optional).</summary>
    private async Task SaveAsAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save logcat output as...",
                Filter = "Text file (*.txt)|*.txt|Log file (*.log)|*.log|All files (*.*)|*.*",
                FileName = $"logcat_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
                InitialDirectory = AppPaths.Logs
            };
            if (dlg.ShowDialog() != true) return;
            var sb = new StringBuilder(AllEntries.Count * 100);
            foreach (var e in AllEntries) sb.AppendLine(e.Raw);
            await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"✓ Saved: {dlg.FileName}";
        }
        catch (Exception ex) { StatusMessage = $"✗ {ex.Message}"; }
    }

    private void CopyAll()
    {
        try
        {
            var sb = new StringBuilder(FilteredEntries.Count * 100);
            foreach (var e in FilteredEntries)
                sb.AppendLine(e.Raw);
            Clipboard.SetText(sb.ToString());
            StatusMessage = $"Copied {FilteredEntries.Count} lines.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
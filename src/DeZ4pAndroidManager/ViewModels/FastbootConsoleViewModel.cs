// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
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

public class FastbootConsoleViewModel : INotifyPropertyChanged
{
    private readonly ConsoleService _console;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private string _commandText = "";
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    public FastbootConsoleViewModel(ConsoleService console, DeviceStateService deviceState,
                                     DeviceWatcherService watcher)
    {
        _console = console;
        _deviceState = deviceState;

        AllPresets = new ObservableCollection<PresetCommand>(ConsoleService.GetFastbootPresets());
        FilteredPresets = new ObservableCollection<PresetCommand>(AllPresets);

        RunCommand = new AsyncRelayCommand(RunAsync);
        ClearOutputCommand = new RelayCommand(_ => { Entries.Clear(); StatusMessage = "Cleared."; });
        CopyOutputCommand = new RelayCommand(_ => CopyOutput());
        SaveOutputCommand = new RelayCommand(_ => SaveOutput());
        HistoryUpCommand = new RelayCommand(_ => NavigateHistory(-1));
        HistoryDownCommand = new RelayCommand(_ => NavigateHistory(1));
        UsePresetCommand = new RelayCommand(p => UsePreset(p as PresetCommand));
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());
        ClearHistoryCommand = new RelayCommand(_ => { _history.Clear(); StatusMessage = "History cleared."; });
        FilterPresetCommand = new RelayCommand(p => FilterPresets(p as string));

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);

        RefreshDevices();

        AppendEntry(ConsoleEntryKind.Info, "Fastboot Console ready. Presets auto-run. Type 'fastboot xxx' or plain command.");
    }

    public ObservableCollection<ConsoleEntry> Entries { get; } = new();
    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<PresetCommand> AllPresets { get; }
    public ObservableCollection<PresetCommand> FilteredPresets { get; }

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); OnPropertyChanged(nameof(HasFastbootDevice)); OnPropertyChanged(nameof(DeviceLabel)); }
    }

    public bool HasDevice => _selectedDevice != null;
    public bool HasFastbootDevice => _selectedDevice?.IsFastboot == true;
    public string DeviceLabel => _selectedDevice?.DisplayName ?? "No device";

    public string CommandText
    {
        get => _commandText;
        set { _commandText = value ?? ""; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public ICommand RunCommand { get; }
    public ICommand ClearOutputCommand { get; }
    public ICommand CopyOutputCommand { get; }
    public ICommand SaveOutputCommand { get; }
    public ICommand HistoryUpCommand { get; }
    public ICommand HistoryDownCommand { get; }
    public ICommand UsePresetCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand FilterPresetCommand { get; }

    private void RefreshDevices()
    {
        var cur = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var w = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (w != null)
                foreach (var d in w.CurrentDevices.Where(x => x.IsFastboot))
                    Devices.Add(d);
        }
        catch { }

        if (!string.IsNullOrEmpty(cur))
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];
    }

    private void AppendEntry(ConsoleEntryKind kind, string text, long durationMs = 0)
    {
        Entries.Add(new ConsoleEntry { Kind = kind, Text = text ?? "", Time = DateTime.Now, DurationMs = durationMs });
        while (Entries.Count > 2000) Entries.RemoveAt(0);
        OnPropertyChanged(nameof(Entries));
    }

    private void AppendBatch(ConsoleEntryKind kind, IEnumerable<string> lines)
    {
        int added = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            Entries.Add(new ConsoleEntry { Kind = kind, Text = line, Time = DateTime.Now });
            added++;
        }
        while (Entries.Count > 2000) Entries.RemoveAt(0);
        if (added > 0) OnPropertyChanged(nameof(Entries));
    }

    private async Task RunAsync()
    {
        var cmd = CommandText?.Trim() ?? "";
        if (string.IsNullOrEmpty(cmd)) return;

        if (SelectedDevice == null)
        {
            StatusMessage = "No fastboot device.";
            AppendEntry(ConsoleEntryKind.Error, "No fastboot device detected. Reboot your device into bootloader.");
            return;
        }

        if (_history.Count == 0 || _history[0] != cmd) _history.Insert(0, cmd);
        while (_history.Count > 200) _history.RemoveAt(_history.Count - 1);
        _historyIndex = -1;

        IsBusy = true;
        try
        {
            AppendEntry(ConsoleEntryKind.Command, cmd);

            // ⭐ auto-strip leading "fastboot "
            var cmdToRun = cmd;
            if (cmdToRun.StartsWith("fastboot ", StringComparison.OrdinalIgnoreCase))
                cmdToRun = cmdToRun.Substring(9).Trim();

            var result = await _console.RunFastbootAsync(SelectedDevice.Serial, cmdToRun);

            if (!string.IsNullOrEmpty(result.StdOut))
                AppendBatch(ConsoleEntryKind.Output,
                    result.StdOut.Replace("\r\n", "\n").Split('\n'));

            if (!string.IsNullOrEmpty(result.StdErr))
                AppendBatch(ConsoleEntryKind.Error,
                    result.StdErr.Replace("\r\n", "\n").Split('\n'));

            if (string.IsNullOrEmpty(result.StdOut) && string.IsNullOrEmpty(result.StdErr))
                AppendEntry(ConsoleEntryKind.Success, "(no output)");

            StatusMessage = $"Done in {result.DurationMs}ms (exit {result.ExitCode})";
        }
        catch (Exception ex)
        {
            AppendEntry(ConsoleEntryKind.Error, ex.Message);
            StatusMessage = "Error.";
        }
        finally { IsBusy = false; }
    }

    private void UsePreset(PresetCommand? preset)
    {
        if (preset == null) return;
        CommandText = preset.Command;
        _ = RunAsync();
    }

    private void FilterPresets(string? category)
    {
        FilteredPresets.Clear();
        foreach (var p in AllPresets)
        {
            if (string.IsNullOrEmpty(category) || p.Category == category)
                FilteredPresets.Add(p);
        }
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;
        _historyIndex += direction;
        if (_historyIndex < 0) _historyIndex = 0;
        if (_historyIndex >= _history.Count) _historyIndex = _history.Count - 1;
        CommandText = _history[_historyIndex];
    }

    private void CopyOutput()
    {
        try
        {
            var sb = new StringBuilder(Entries.Count * 40);
            foreach (var e in Entries)
                sb.AppendLine($"[{e.TimeDisplay}] {e.Prefix} {e.Text}");
            Clipboard.SetText(sb.ToString());
            StatusMessage = $"Copied {Entries.Count} lines.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    /// <summary>Auto-save to Documents\DeZ4p Android Manager\Console Output\</summary>
    private void SaveOutput()
    {
        try
        {
            var folder = AppPaths.ConsoleOutput;
            Directory.CreateDirectory(folder);
            var fname = $"fastboot_console_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            var path = Path.Combine(folder, fname);

            var sb = new StringBuilder(Entries.Count * 40);
            foreach (var e in Entries)
                sb.AppendLine($"[{e.TimeDisplay}] {e.Prefix} {e.Text}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"✓ Saved: Console Output\\{fname}";
        }
        catch (Exception ex) { StatusMessage = $"✗ {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
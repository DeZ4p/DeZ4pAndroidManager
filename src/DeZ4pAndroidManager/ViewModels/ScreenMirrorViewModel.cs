// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class ScreenMirrorViewModel : INotifyPropertyChanged
{
    private readonly ScreenMirrorService _mirror;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private bool _isRunning;
    private string _statusMessage = "Ready.";
    private string _scrcpyVersion = "checking...";

    private int _maxSize = 1024;
    private int _videoBitRateMbps = 8;
    private int _maxFps = 60;
    private bool _fullscreen;
    private bool _alwaysOnTop;
    private bool _turnScreenOff;
    private bool _stayAwake;
    private bool _showTouches = true;
    private bool _muteAudio = true;
    private bool _audioOnly;
    private bool _record;
    private string _recordPath = "";
    private string _videoCodec = "(default)";
    private string _keyboardMode = "(default)";
    private string _crop = "";
    private string _windowTitle = "DeZ4p Screen Mirror";

    public ScreenMirrorViewModel(ScreenMirrorService mirror, DeviceStateService deviceState,
                                  DeviceWatcherService watcher)
    {
        _mirror = mirror;
        _deviceState = deviceState;

        StartCommand = new AsyncRelayCommand(StartAsync);
        StopCommand = new RelayCommand(_ => Stop());
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());
        OpenScrcpyFolderCommand = new RelayCommand(_ => _mirror.OpenScrcpyFolder());
        CheckScrcpyCommand = new AsyncRelayCommand(CheckScrcpyAsync);
        BrowseRecordPathCommand = new RelayCommand(_ => BrowseRecordPath());
        OpenRecordFolderCommand = new RelayCommand(_ => OpenRecordFolder());
        ClearLogCommand = new RelayCommand(_ => LogText = "");

        // Central recording folder under Documents\DeZ4p Android Manager\Screen Mirror Recordings
        try
        {
            var folder = AppPaths.ScreenMirrorRecordings;
            RecordPath = Path.Combine(folder, $"mirror_{DateTime.Now:yyyy-MM-dd_HH-mm}.mp4");
        }
        catch
        {
            RecordPath = Path.Combine(Path.GetTempPath(),
                $"dez4p_mirror_{DateTime.Now:yyyy-MM-dd_HH-mm}.mp4");
        }

        _mirror.OutputReceived += (_, line) => Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                AppendLog(line);
                StatusMessage = line.Trim();
            }
        });
        _mirror.MirrorStarted += (_, _) => Application.Current?.Dispatcher.Invoke(() =>
        {
            IsRunning = true;
        });
        _mirror.MirrorExited += (_, code) => Application.Current?.Dispatcher.Invoke(() =>
        {
            IsRunning = false;
            AppendLog($"--- scrcpy exited (code {code}) ---");
            StatusMessage = $"scrcpy exited (code {code}).";
        });

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);

        _ = CheckScrcpyAsync();
        RefreshDevices();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();

    // ═══ LOG ═══
    private string _logText = "";
    public string LogText
    {
        get => _logText;
        set { _logText = value ?? ""; OnPropertyChanged(); }
    }

    private void AppendLog(string line)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogText += $"[{ts}] {line}\n";
        if (LogText.Length > 20000) LogText = LogText.Substring(LogText.Length - 15000);
    }

    // ═══ DEVICE ═══
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

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public string ScrcpyVersion
    {
        get => _scrcpyVersion;
        set
        {
            _scrcpyVersion = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScrcpyStatus));
            OnPropertyChanged(nameof(ScrcpyStatusColor));
        }
    }

    public string ScrcpyStatus => _mirror.IsScrcpyAvailable
        ? $"✓ scrcpy detected  -  {_scrcpyVersion}"
        : "✗ scrcpy.exe not found - click 'scrcpy folder' to install";
    public string ScrcpyStatusColor => _mirror.IsScrcpyAvailable ? "#34D399" : "#EF4444";

    // ═══ OPTIONS ═══
    public int MaxSize { get => _maxSize; set { _maxSize = value; OnPropertyChanged(); } }
    public int VideoBitRateMbps { get => _videoBitRateMbps; set { _videoBitRateMbps = Math.Max(1, Math.Min(50, value)); OnPropertyChanged(); } }
    public int MaxFps { get => _maxFps; set { _maxFps = value; OnPropertyChanged(); } }
    public bool Fullscreen { get => _fullscreen; set { _fullscreen = value; OnPropertyChanged(); } }
    public bool AlwaysOnTop { get => _alwaysOnTop; set { _alwaysOnTop = value; OnPropertyChanged(); } }
    public bool TurnScreenOff { get => _turnScreenOff; set { _turnScreenOff = value; OnPropertyChanged(); } }
    public bool StayAwake { get => _stayAwake; set { _stayAwake = value; OnPropertyChanged(); } }
    public bool ShowTouches { get => _showTouches; set { _showTouches = value; OnPropertyChanged(); } }
    public bool MuteAudio { get => _muteAudio; set { _muteAudio = value; OnPropertyChanged(); } }
    public bool AudioOnly { get => _audioOnly; set { _audioOnly = value; OnPropertyChanged(); } }
    public bool Record { get => _record; set { _record = value; OnPropertyChanged(); } }
    public string RecordPath { get => _recordPath; set { _recordPath = value ?? ""; OnPropertyChanged(); } }
    public string VideoCodec { get => _videoCodec; set { _videoCodec = value ?? ""; OnPropertyChanged(); } }
    public string KeyboardMode { get => _keyboardMode; set { _keyboardMode = value ?? ""; OnPropertyChanged(); } }
    public string Crop { get => _crop; set { _crop = value ?? ""; OnPropertyChanged(); } }
    public string WindowTitle { get => _windowTitle; set { _windowTitle = value ?? ""; OnPropertyChanged(); } }

    public ObservableCollection<ResolutionPreset> ResolutionPresets { get; } = new()
    {
        new("Native", 0),
        new("1920 (Full HD)", 1920),
        new("1280 (HD)", 1280),
        new("1024 (Default)", 1024),
        new("800", 800),
        new("640", 640),
        new("480 (Low)", 480)
    };

    public ObservableCollection<FpsPreset> FpsPresets { get; } = new()
    {
        new("Unlimited", 0),
        new("15 fps", 15),
        new("24 fps", 24),
        new("30 fps", 30),
        new("60 fps", 60),
        new("90 fps", 90),
        new("120 fps", 120)
    };

    public ObservableCollection<string> CodecOptions { get; } = new() { "(default)", "h264", "h265", "av1" };
    public ObservableCollection<string> KeyboardOptions { get; } = new() { "(default)", "sdk", "uhid", "aoa" };

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand OpenScrcpyFolderCommand { get; }
    public ICommand CheckScrcpyCommand { get; }
    public ICommand BrowseRecordPathCommand { get; }
    public ICommand OpenRecordFolderCommand { get; }
    public ICommand ClearLogCommand { get; }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════
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

    private async Task CheckScrcpyAsync()
    {
        ScrcpyVersion = "checking...";
        var ver = await _mirror.GetScrcpyVersionAsync();
        ScrcpyVersion = ver;
        AppendLog($"scrcpy version: {ver}");
    }

    private void BrowseRecordPath()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Save recording as...",
            Filter = "MP4 video (*.mp4)|*.mp4|MKV video (*.mkv)|*.mkv|All files (*.*)|*.*",
            FileName = Path.GetFileName(RecordPath)
        };
        try
        {
            var dir = Path.GetDirectoryName(RecordPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) sfd.InitialDirectory = dir;
        }
        catch { }
        if (sfd.ShowDialog() == true) RecordPath = sfd.FileName;
    }

    private void OpenRecordFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(RecordPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════
    //  START (with version-aware args + fallback + error display)
    // ═══════════════════════════════════════════════════════════
    private async Task StartAsync()
    {
        if (_mirror.IsRunning) { StatusMessage = "Already running."; return; }

        if (!_mirror.IsScrcpyAvailable)
        {
            StatusMessage = $"✗ scrcpy.exe not found at: {_mirror.ScrcpyPath}";
            var open = MessageBox.Show(
                $"scrcpy.exe was not found at:\n\n{_mirror.ScrcpyPath}\n\n" +
                "Download scrcpy from:\nhttps://github.com/Genymobile/scrcpy/releases\n\n" +
                "Open the folder now?",
                "scrcpy not found", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (open == MessageBoxResult.Yes) _mirror.OpenScrcpyFolder();
            return;
        }

        if (SelectedDevice == null)
        {
            StatusMessage = "✗ No device selected.";
            MessageBox.Show("No device selected.\n\nConnect a device via USB with USB debugging enabled.",
                "No device", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (Record && string.IsNullOrEmpty(RecordPath))
        {
            BrowseRecordPath();
            if (string.IsNullOrEmpty(RecordPath)) return;
        }

        var opts = new ScreenMirrorOptions
        {
            Serial = SelectedDevice.Serial,
            MaxSize = MaxSize,
            VideoBitRateMbps = VideoBitRateMbps,
            MaxFps = MaxFps,
            Fullscreen = Fullscreen,
            AlwaysOnTop = AlwaysOnTop,
            TurnScreenOff = TurnScreenOff,
            StayAwake = StayAwake,
            ShowTouches = ShowTouches,
            NoAudio = MuteAudio,
            AudioOnly = AudioOnly && !MuteAudio,
            Record = Record,
            RecordPath = RecordPath,
            WindowTitle = WindowTitle,
            VideoCodec = NormalizeCombo(VideoCodec),
            KeyboardMode = NormalizeCombo(KeyboardMode),
            Crop = Crop
        };

        LogText = "";
        AppendLog($"Starting mirror for {SelectedDevice.DisplayName}");
        StatusMessage = "Starting...";

        try
        {
            var (ok, error) = await _mirror.StartWithFallbackAsync(opts);

            if (!ok)
            {
                StatusMessage = "✗ scrcpy failed to start. See log.";
                AppendLog("");
                AppendLog("=== FAILED ===");
                AppendLog(error);

                MessageBox.Show(
                    "scrcpy exited immediately.\n\n" +
                    "Details are shown in the log panel.\n\n" +
                    "Common causes:\n" +
                    "• Device not authorized (check phone screen)\n" +
                    "• USB debugging disabled\n" +
                    "• scrcpy and adb version mismatch\n" +
                    "• Device not in 'device' state (run: adb devices)\n\n" +
                    "Full error:\n\n" + error,
                    "scrcpy error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsRunning = true;
            StatusMessage = $"▶ Mirroring {SelectedDevice.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error: {ex.Message}";
            AppendLog($"Exception: {ex}");
        }
    }

    private static string NormalizeCombo(string v)
    {
        if (string.IsNullOrWhiteSpace(v) || v == "(default)") return "";
        return v;
    }

    private void Stop()
    {
        _mirror.Stop();
        IsRunning = false;
        StatusMessage = "Mirror stopped.";
        AppendLog("Stopped by user");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ResolutionPreset
{
    public ResolutionPreset(string name, int value) { Name = name; Value = value; }
    public string Name { get; }
    public int Value { get; }
}

public class FpsPreset
{
    public FpsPreset(string name, int value) { Name = name; Value = value; }
    public string Name { get; }
    public int Value { get; }
}
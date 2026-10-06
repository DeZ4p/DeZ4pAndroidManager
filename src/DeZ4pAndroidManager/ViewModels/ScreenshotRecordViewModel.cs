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

public class ScreenshotRecordViewModel : INotifyPropertyChanged
{
    private readonly ScreenshotRecordService _svc;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private string _outputFolder = "";
    private bool _saveAsJpg = true;
    private int _delaySeconds = 0;
    private bool _autoOpen = true;

    // Recording
    private int _recWidth = 0;
    private int _recHeight = 0;
    private int _recBitRateMbps = 8;
    private int _recTimeLimitSec = 180;
    private bool _recRotate = false;
    private bool _recAudio = false;
    private string _recAudioSource = "mic";
    private bool _isRecording;

    // Version check
    private int _apiLevel = 0;
    private string _androidVersion = "?";
    private bool _isVersionChecked = false;

    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private string _logText = "";

    public ScreenshotRecordViewModel(ScreenshotRecordService svc, DeviceStateService deviceState,
                                      DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;

        // Central output folder under Documents\DeZ4p Android Manager\Screenshots
        OutputFolder = AppPaths.Screenshots;

        TakeScreenshotCommand = new AsyncRelayCommand(TakeScreenshotAsync);
        StartRecordingCommand = new AsyncRelayCommand(StartRecordingAsync);
        StopRecordingCommand = new AsyncRelayCommand(StopRecordingAsync);
        RefreshDevicesCommand = new AsyncRelayCommand(CheckDeviceAsync);
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        ClearLogCommand = new RelayCommand(_ => LogText = "");

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() => _ = CheckDeviceAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() => _ = CheckDeviceAsync());

        _ = CheckDeviceAsync();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<CaptureItem> RecentCaptures { get; } = new();

    // ═══ DEVICE ═══
    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }

    public bool HasDevice => _selectedDevice != null;

    // ═══ VERSION ═══
    public int ApiLevel
    {
        get => _apiLevel;
        set { _apiLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsVersionSupported)); OnPropertyChanged(nameof(VersionWarning)); OnPropertyChanged(nameof(HasVersionWarning)); }
    }

    public string AndroidVersion
    {
        get => _androidVersion;
        set { _androidVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(VersionWarning)); }
    }

    public bool IsVersionChecked
    {
        get => _isVersionChecked;
        set { _isVersionChecked = value; OnPropertyChanged(); }
    }

    // User wants Android 11+ (API 30+)
    public bool IsVersionSupported => _apiLevel >= 30;

    public bool HasVersionWarning => _isVersionChecked && !IsVersionSupported;

    public string VersionWarning => string.Format(LocalizationService.Translate("ScrRec.VersionWarningMsg"), _androidVersion, _apiLevel);

    // ═══ OPTIONS ═══
    public string OutputFolder
    {
        get => _outputFolder;
        set { _outputFolder = value ?? ""; OnPropertyChanged(); }
    }

    public bool SaveAsJpg
    {
        get => _saveAsJpg;
        set { _saveAsJpg = value; OnPropertyChanged(); }
    }

    public int DelaySeconds
    {
        get => _delaySeconds;
        set { _delaySeconds = value; OnPropertyChanged(); }
    }

    public bool AutoOpen
    {
        get => _autoOpen;
        set { _autoOpen = value; OnPropertyChanged(); }
    }

    public int RecWidth
    {
        get => _recWidth;
        set { _recWidth = value; OnPropertyChanged(); }
    }

    public int RecHeight
    {
        get => _recHeight;
        set { _recHeight = value; OnPropertyChanged(); }
    }

    public int RecBitRateMbps
    {
        get => _recBitRateMbps;
        set { _recBitRateMbps = value; OnPropertyChanged(); }
    }

    public int RecTimeLimitSec
    {
        get => _recTimeLimitSec;
        set { _recTimeLimitSec = value; OnPropertyChanged(); }
    }

    public bool RecRotate
    {
        get => _recRotate;
        set { _recRotate = value; OnPropertyChanged(); }
    }

    public bool RecAudio
    {
        get => _recAudio;
        set { _recAudio = value; OnPropertyChanged(); }
    }

    public string RecAudioSource
    {
        get => _recAudioSource;
        set { _recAudioSource = value ?? "mic"; OnPropertyChanged(); }
    }

    public bool IsRecording
    {
        get => _isRecording;
        set { _isRecording = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartRecord)); }
    }

    public bool CanStartRecord => HasDevice && IsVersionSupported && !IsRecording;

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

    public string LogText
    {
        get => _logText;
        set { _logText = value ?? ""; OnPropertyChanged(); }
    }

    // Presets
    public ObservableCollection<DelayPreset> DelayOptions { get; } = new()
    {
        new("No delay", 0),
        new("3 sec", 3),
        new("5 sec", 5),
        new("10 sec", 10)
    };

    public ObservableCollection<SizePreset> SizeOptions { get; } = new()
    {
        new("Native (default)", 0, 0),
        new("1280 × 720 (HD)", 1280, 720),
        new("1920 × 1080 (FHD)", 1920, 1080),
        new("720 × 1280 (Portrait HD)", 720, 1280)
    };

    public ObservableCollection<int> BitrateOptions { get; } = new() { 2, 4, 8, 12, 16, 24, 32 };
    public ObservableCollection<TimeLimitPreset> TimeLimitOptions { get; } = new()
    {
        new("30 sec", 30),
        new("60 sec", 60),
        new("120 sec", 120),
        new("180 sec (max)", 180)
    };

    public ObservableCollection<string> AudioSourceOptions { get; } = new() { "mic", "playback", "voice_call", "voice_recognition" };

    // ═══ COMMANDS ═══
    public ICommand TakeScreenshotCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand RefreshDevicesCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ClearLogCommand { get; }

    // ═══════════════════════════════════════════════════════════
    private async Task CheckDeviceAsync()
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

        if (SelectedDevice != null)
        {
            var (api, ver) = await _svc.GetVersionInfoAsync(SelectedDevice.Serial);
            ApiLevel = api;
            AndroidVersion = ver;
            IsVersionChecked = true;
            AppendLog($"Detected Android {ver} (API {api})");

            if (!IsVersionSupported)
            {
                StatusMessage = $"⚠ نیازمند Android 11+ - دستگاه شما: Android {ver} (API {api})";
                AppendLog("══════════════════════════════════════");
                AppendLog("⚠ VERSION NOT SUPPORTED");
                AppendLog($"   This feature requires Android 11+ (API 30+).");
                AppendLog($"   Your device: Android {ver} (API {api}).");
                AppendLog("══════════════════════════════════════");
            }
            else
            {
                StatusMessage = $"✓ Ready - Android {ver} (API {api})";
            }
        }
        else
        {
            IsVersionChecked = false;
            StatusMessage = "No device connected.";
        }
    }

    private void AppendLog(string line)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogText += $"[{ts}] {line}\n";
        if (LogText.Length > 20000) LogText = LogText.Substring(LogText.Length - 15000);
    }

    private void BrowseFolder()
    {
        var ofd = new OpenFolderDialog { Title = "Choose output folder" };
        if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
            ofd.InitialDirectory = OutputFolder;
        if (ofd.ShowDialog() == true) OutputFolder = ofd.FolderName;
    }

    private void OpenFolder()
    {
        try
        {
            if (string.IsNullOrEmpty(OutputFolder)) return;
            Directory.CreateDirectory(OutputFolder);
            Process.Start(new ProcessStartInfo { FileName = OutputFolder, UseShellExecute = true });
        }
        catch { }
    }

    private async Task TakeScreenshotAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        if (!IsVersionSupported)
        {
            MessageBox.Show(VersionWarning, "Android 11+ required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = DelaySeconds > 0 ? $"Waiting {DelaySeconds}s..." : "Capturing...";
            AppendLog($"Taking screenshot (delay={DelaySeconds}s, jpg={SaveAsJpg})");

            var (ok, local, err) = await _svc.TakeScreenshotAsync(
                SelectedDevice.Serial, OutputFolder, SaveAsJpg, DelaySeconds);

            if (!ok)
            {
                StatusMessage = $"✗ Screenshot failed: {err}";
                AppendLog($"✗ {err}");
                return;
            }

            var fi = new FileInfo(local);
            StatusMessage = $"✓ Screenshot saved: {Path.GetFileName(local)} ({MediaItem.FormatSize(fi.Length)})";
            AppendLog($"✓ Saved: {local}");
            AddRecent(local, "Screenshot");

            if (AutoOpen)
            {
                try { Process.Start(new ProcessStartInfo { FileName = local, UseShellExecute = true }); }
                catch { }
            }
        }
        finally { IsBusy = false; }
    }

    private async Task StartRecordingAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "No device."; return; }
        if (!IsVersionSupported)
        {
            MessageBox.Show(VersionWarning, "Android 11+ required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            AppendLog($"Starting recording ({RecWidth}x{RecHeight}, {RecBitRateMbps}Mbps, {RecTimeLimitSec}s)");

            var opts = new ScreenshotRecordOptions
            {
                Serial = SelectedDevice.Serial,
                OutputFolder = OutputFolder,
                RecordWidth = RecWidth,
                RecordHeight = RecHeight,
                RecordBitRateMbps = RecBitRateMbps,
                RecordTimeLimitSec = RecTimeLimitSec,
                RecordRotate = RecRotate,
                RecordAudio = RecAudio,
                RecordAudioSource = RecAudioSource
            };

            var (ok, err) = await _svc.StartRecordingAsync(SelectedDevice.Serial, OutputFolder, opts);

            if (!ok)
            {
                StatusMessage = $"✗ Start failed: {err}";
                AppendLog($"✗ {err}");
                return;
            }

            IsRecording = true;
            StatusMessage = "● Recording...";
            AppendLog("● Recording started. Click Stop to finalize.");
        }
        finally { IsBusy = false; }
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording) return;

        IsBusy = true;
        try
        {
            StatusMessage = "Stopping...";
            AppendLog("Stopping recording...");

            var (ok, local, err) = await _svc.StopRecordingAsync();

            IsRecording = false;

            if (!ok)
            {
                StatusMessage = $"✗ Stop failed: {err}";
                AppendLog($"✗ {err}");
                return;
            }

            var fi = new FileInfo(local);
            StatusMessage = $"✓ Recording saved: {Path.GetFileName(local)} ({MediaItem.FormatSize(fi.Length)})";
            AppendLog($"✓ Saved: {local}");
            AddRecent(local, "Recording");

            if (AutoOpen)
            {
                try { Process.Start(new ProcessStartInfo { FileName = local, UseShellExecute = true }); }
                catch { }
            }
        }
        finally { IsBusy = false; }
    }

    private void AddRecent(string path, string kind)
    {
        try
        {
            var fi = new FileInfo(path);
            RecentCaptures.Insert(0, new CaptureItem
            {
                Path = path,
                FileName = fi.Name,
                Kind = kind,
                SizeBytes = fi.Length,
                CreatedAt = fi.CreationTime
            });
            while (RecentCaptures.Count > 30) RecentCaptures.RemoveAt(RecentCaptures.Count - 1);
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class DelayPreset
{
    public DelayPreset(string name, int value) { Name = name; Value = value; }
    public string Name { get; }
    public int Value { get; }
}

public class SizePreset
{
    public SizePreset(string name, int w, int h) { Name = name; Width = w; Height = h; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
}

public class TimeLimitPreset
{
    public TimeLimitPreset(string name, int value) { Name = name; Value = value; }
    public string Name { get; }
    public int Value { get; }
}

public class CaptureItem
{
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Kind { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    public string SizeDisplay => MediaItem.FormatSize(SizeBytes);
    public string CreatedDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string InfoDisplay => $"{Kind} - {SizeDisplay}";
}
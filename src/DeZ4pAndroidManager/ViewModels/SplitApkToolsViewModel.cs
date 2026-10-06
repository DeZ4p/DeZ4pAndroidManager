// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class SplitApkToolsViewModel : INotifyPropertyChanged
{
    private readonly SplitApkService _split;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private string _mode = "analyze";
    private string _sourcePath = "";
    private string _outputPath = "";
    private bool _isLoading;
    private bool _isBusy;
    private double _progress;
    private string _statusMessage = "Ready.";
    private SplitApkSet? _currentSet;
    private DeviceModel? _selectedDevice;

    private CancellationTokenSource? _busyCts;

    public SplitApkToolsViewModel(SplitApkService split, DeviceStateService deviceState,
                                   DeviceWatcherService watcher, ActivityLogService activity)
    {
        _split = split;
        _deviceState = deviceState;
        _activity = activity;

        SetModeCommand = new RelayCommand(p => SetMode(p as string ?? "analyze"));
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        BrowseOutputFolderCommand = new RelayCommand(_ => BrowseOutputFolder());
        ClearSourceCommand = new RelayCommand(_ => ClearSource());
        OpenOutputCommand = new RelayCommand(_ => OpenOutput());
        CancelCommand = new RelayCommand(_ => CancelBusy());
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);

        RefreshDevices();
        StatusMessage = "Analyze mode. Press the big button or drag a .apks / folder here.";
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<SplitFileInfo> Splits { get; } = new();

    // ═══ MODE ═══
    public string Mode
    {
        get => _mode;
        set
        {
            _mode = value ?? "analyze";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnalyzeMode));
            OnPropertyChanged(nameof(IsPackMode));
            OnPropertyChanged(nameof(IsUnpackMode));
            OnPropertyChanged(nameof(IsInstallMode));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(PrimaryActionIcon));
            OnPropertyChanged(nameof(PrimaryActionColor));
            OnPropertyChanged(nameof(PrimaryActionBrush));
            OnPropertyChanged(nameof(PrimaryActionHint));
        }
    }

    public bool IsAnalyzeMode => Mode == "analyze";
    public bool IsPackMode => Mode == "pack";
    public bool IsUnpackMode => Mode == "unpack";
    public bool IsInstallMode => Mode == "install";

    public string PrimaryActionLabel => Mode switch
    {
        "pack" => LocalizationService.Translate("SplitTools.ModePack"),
        "unpack" => LocalizationService.Translate("SplitTools.ModeUnpack"),
        "install" => LocalizationService.Translate("SplitTools.ModeInstall"),
        _ => LocalizationService.Translate("SplitTools.ModeAnalyze")
    };

    public string PrimaryActionHint => Mode switch
    {
        "pack" => "Pick a folder with .apk files, then click Pack.",
        "unpack" => "Pick a .apks file, then click Unpack.",
        "install" => "Pick a .apks file or folder, then click Install.",
        _ => "Pick a .apks file or a folder of splits, then click Analyze."
    };

    public string PrimaryActionIcon => Mode switch
    {
        "pack" => "\uE8C8",
        "unpack" => "\uE8B7",
        "install" => "\uE896",
        _ => "\uE9D9"
    };

    public string PrimaryActionColor => Mode switch
    {
        "pack" => "#A855F7",
        "unpack" => "#22D3EE",
        "install" => "#34D399",
        _ => "#4F8CFF"
    };

    public Brush PrimaryActionBrush
    {
        get
        {
            try
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(PrimaryActionColor));
                b.Freeze();
                return b;
            }
            catch { return Brushes.DeepSkyBlue; }
        }
    }

    // ═══ SOURCE / OUTPUT ═══
    public string SourcePath
    {
        get => _sourcePath;
        set { _sourcePath = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasSource)); }
    }

    public bool HasSource => !string.IsNullOrEmpty(_sourcePath);

    public string OutputPath
    {
        get => _outputPath;
        set { _outputPath = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasOutput)); }
    }

    public bool HasOutput => !string.IsNullOrEmpty(_outputPath);

    // ═══ STATE ═══
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

    public double Progress
    {
        get => _progress;
        set { _progress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public SplitApkSet? CurrentSet
    {
        get => _currentSet;
        set
        {
            _currentSet = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSet));
            OnPropertyChanged(nameof(HasValidSet));
            OnPropertyChanged(nameof(SetName));
            OnPropertyChanged(nameof(SetTotalSize));
            OnPropertyChanged(nameof(SetSplitCount));
            OnPropertyChanged(nameof(SetArchSummary));
            OnPropertyChanged(nameof(SetLangSummary));
            OnPropertyChanged(nameof(SetDensitySummary));
            OnPropertyChanged(nameof(SetSourceKind));
        }
    }

    public bool HasSet => _currentSet != null && _currentSet.Splits.Count > 0;
    public bool HasValidSet => HasSet && _currentSet!.HasBase;

    public string SetName => _currentSet?.InferredName ?? "-";
    public string SetTotalSize => _currentSet?.TotalSizeDisplay ?? "-";
    public string SetSplitCount => _currentSet?.SplitCountDisplay ?? "-";
    public string SetArchSummary => _currentSet?.ArchSummary ?? "-";
    public string SetLangSummary => _currentSet?.LangSummary ?? "-";
    public string SetDensitySummary => _currentSet?.DensitySummary ?? "-";
    public string SetSourceKind => _currentSet == null ? "" :
        (_currentSet.IsApksFile ? "APKS Bundle" : "Folder of splits");

    // ═══ DEVICE ═══
    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }

    public bool HasDevice => _selectedDevice != null;

    // ═══ COMMANDS (kept for other buttons) ═══
    public ICommand SetModeCommand { get; }
    public ICommand BrowseFileCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand ClearSourceCommand { get; }
    public ICommand OpenOutputCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshDevicesCommand { get; }

    // ═══════════════════════════════════════════════════════════
    //  MAIN PUBLIC METHOD - called directly from code-behind
    // ═══════════════════════════════════════════════════════════
    public async Task PrimaryActionAsync()
    {
        try
        {
            StatusMessage = $"▶ {PrimaryActionLabel}...";

            // Step 1: ensure source
            if (!HasSource)
            {
                var picked = await PickSourceAsync();
                if (!picked)
                {
                    StatusMessage = "No source selected.";
                    return;
                }
            }

            // Step 2: run the correct action
            switch (Mode)
            {
                case "pack": await RunPackAsync(); break;
                case "unpack": await RunUnpackAsync(); break;
                case "install": await RunInstallAsync(); break;
                default: await RunAnalyzeAsync(); break;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>Opens the appropriate dialog and loads a source.</summary>
    private async Task<bool> PickSourceAsync()
    {
        await Task.Yield(); // ensure we're on UI thread
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (IsPackMode)
                {
                    var ofd = new OpenFolderDialog { Title = "Select folder with .apk splits" };
                    if (ofd.ShowDialog() != true) return false;
                    SetSourcePath(ofd.FolderName);
                    return true;
                }

                if (IsUnpackMode)
                {
                    var ofd = new OpenFileDialog
                    {
                        Title = "Select .apks file",
                        Filter = "Split APK Bundle (*.apks)|*.apks|All files (*.*)|*.*"
                    };
                    if (ofd.ShowDialog() != true) return false;
                    SetSourcePath(ofd.FileName);
                    return true;
                }

                if (IsInstallMode)
                {
                    // Ask: .apks file or folder?
                    var r = MessageBox.Show(
                        "Install from .apks file?\n\nYES = pick a .apks file\nNO  = pick a folder of splits",
                        "Install source",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);
                    if (r == MessageBoxResult.Cancel) return false;

                    if (r == MessageBoxResult.Yes)
                    {
                        var ofd = new OpenFileDialog
                        {
                            Title = "Select .apks file",
                            Filter = "Split APK Bundle (*.apks)|*.apks|All files (*.*)|*.*"
                        };
                        if (ofd.ShowDialog() != true) return false;
                        SetSourcePath(ofd.FileName);
                        return true;
                    }
                    else
                    {
                        var ofd = new OpenFolderDialog { Title = "Select folder with .apk splits" };
                        if (ofd.ShowDialog() != true) return false;
                        SetSourcePath(ofd.FolderName);
                        return true;
                    }
                }

                // Analyze
                var result = MessageBox.Show(
                    "Analyze .apks file?\n\nYES = pick a .apks file\nNO  = pick a folder of splits",
                    "Analyze source",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel) return false;

                if (result == MessageBoxResult.Yes)
                {
                    var ofd = new OpenFileDialog
                    {
                        Title = "Select .apks file",
                        Filter = "Split APK Bundle (*.apks)|*.apks|All files (*.*)|*.*"
                    };
                    if (ofd.ShowDialog() != true) return false;
                    SetSourcePath(ofd.FileName);
                    return true;
                }
                else
                {
                    var ofd = new OpenFolderDialog { Title = "Select folder with .apk splits" };
                    if (ofd.ShowDialog() != true) return false;
                    SetSourcePath(ofd.FolderName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Dialog error: {ex.Message}";
                return false;
            }
        });
    }

    private void SetSourcePath(string path)
    {
        SourcePath = path;
        StatusMessage = $"Loaded: {path}";
    }

    // ═══════════════════════════════════════════════════════════
    //  HANDLE DRAG & DROP
    // ═══════════════════════════════════════════════════════════
    public void HandleDroppedPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (Directory.Exists(path))
        {
            SetSourcePath(path);
            _ = RunAnalyzeAsync();
            return;
        }

        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".apks" || ext == ".apk")
            {
                SetSourcePath(path);
                _ = RunAnalyzeAsync();
                return;
            }
            StatusMessage = $"Unsupported file: {ext}. Use .apks or a folder.";
            return;
        }

        StatusMessage = "Path not found.";
    }

    // ═══════════════════════════════════════════════════════════
    //  MODE / BROWSERS / HELPERS
    // ═══════════════════════════════════════════════════════════
    private void SetMode(string mode)
    {
        Mode = mode;
        StatusMessage = PrimaryActionHint;

        // Pre-fill default output folder per mode
        try
        {
            if (mode == "pack" && string.IsNullOrEmpty(OutputPath))
            {
                var name = string.IsNullOrEmpty(SourcePath)
                    ? "app.apks"
                    : Path.GetFileNameWithoutExtension(SourcePath) + ".apks";
                OutputPath = Path.Combine(AppPaths.SplitApkPacked, name);
            }
            else if (mode == "unpack" && string.IsNullOrEmpty(OutputPath))
            {
                OutputPath = AppPaths.SplitApkUnpacked;
            }
        }
        catch { }
    }

    public void BrowseFile()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select .apks file",
            Filter = "Split APK Bundle (*.apks)|*.apks|Single APK (*.apk)|*.apk|All files (*.*)|*.*"
        };
        if (ofd.ShowDialog() == true) SetSourcePath(ofd.FileName);
    }

    public void BrowseFolder()
    {
        var ofd = new OpenFolderDialog { Title = "Select folder with .apk splits" };
        if (ofd.ShowDialog() == true) SetSourcePath(ofd.FolderName);
    }

    private void BrowseOutputFolder()
    {
        if (IsPackMode)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save .apks file",
                Filter = "Split APK Bundle (*.apks)|*.apks",
                FileName = string.IsNullOrEmpty(SourcePath)
                    ? "app.apks"
                    : Path.GetFileNameWithoutExtension(SourcePath) + ".apks"
            };
            if (sfd.ShowDialog() == true) OutputPath = sfd.FileName;
        }
        else
        {
            var ofd = new OpenFolderDialog { Title = "Choose output folder" };
            if (ofd.ShowDialog() == true) OutputPath = ofd.FolderName;
        }
    }

    private void ClearSource()
    {
        SourcePath = "";
        CurrentSet = null;
        Splits.Clear();
        Progress = 0;
        StatusMessage = PrimaryActionHint;
    }

    private void OpenOutput()
    {
        if (string.IsNullOrEmpty(OutputPath)) return;
        try
        {
            if (File.Exists(OutputPath))
                Process.Start("explorer.exe", $"/select,\"{OutputPath}\"");
            else if (Directory.Exists(OutputPath))
                Process.Start(new ProcessStartInfo { FileName = OutputPath, UseShellExecute = true });
            else
            {
                var dir = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
        }
        catch { }
    }

    private void CancelBusy()
    {
        try { _busyCts?.Cancel(); } catch { }
        StatusMessage = "Cancel requested...";
    }

    private void RefreshDevices()
    {
        var currentSerial = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var watcher = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (watcher != null)
                foreach (var d in watcher.CurrentDevices.Where(x => x.IsReady))
                    Devices.Add(d);
        }
        catch { }

        if (!string.IsNullOrEmpty(currentSerial))
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == currentSerial);
        if (SelectedDevice == null && Devices.Count > 0)
            SelectedDevice = Devices[0];
    }

    // ═══════════════════════════════════════════════════════════
    //  ACTIONS
    // ═══════════════════════════════════════════════════════════
    private async Task RunAnalyzeAsync()
    {
        if (string.IsNullOrEmpty(SourcePath)) return;

        IsLoading = true;
        StatusMessage = "Analyzing...";
        try
        {
            var set = await _split.AnalyzeAsync(SourcePath);

            if (set == null)
            {
                StatusMessage = "Could not read. Make sure the source is a valid .apks file or a folder with .apk files.";
                CurrentSet = null;
                Splits.Clear();
                return;
            }

            CurrentSet = set;
            Splits.Clear();
            foreach (var s in set.Splits) Splits.Add(s);

            StatusMessage = set.HasBase
                ? $"Analyzed {set.Splits.Count} split(s), total {set.TotalSizeDisplay}"
                : $"Warning: base.apk missing ({set.Splits.Count} splits found)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analyze failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task RunPackAsync()
    {
        if (string.IsNullOrEmpty(SourcePath) || !Directory.Exists(SourcePath))
        {
            StatusMessage = "Source folder not found.";
            return;
        }
        if (string.IsNullOrEmpty(OutputPath))
        {
            BrowseOutputFolder();
            if (string.IsNullOrEmpty(OutputPath)) return;
        }

        IsBusy = true;
        Progress = 0;
        _busyCts = new CancellationTokenSource();
        try
        {
            Progress = 30;
            StatusMessage = "Packing...";
            var (ok, output, error) = await _split.PackAsync(SourcePath, OutputPath, _busyCts.Token);
            if (ok)
            {
                Progress = 100;
                StatusMessage = $"Packed: {output}";
                _activity.LogInfo($"Packed: {Path.GetFileName(output)}", "PKG", "#A855F7");
            }
            else StatusMessage = $"Pack failed: {error}";
        }
        catch (OperationCanceledException) { StatusMessage = "Cancelled."; }
        catch (Exception ex) { StatusMessage = $"Pack error: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            _busyCts?.Dispose();
            _busyCts = null;
        }
    }

    private async Task RunUnpackAsync()
    {
        if (string.IsNullOrEmpty(SourcePath) || !File.Exists(SourcePath))
        {
            StatusMessage = "Source .apks not found.";
            return;
        }
        if (string.IsNullOrEmpty(OutputPath))
        {
            BrowseOutputFolder();
            if (string.IsNullOrEmpty(OutputPath)) return;
        }

        IsBusy = true;
        Progress = 0;
        _busyCts = new CancellationTokenSource();
        try
        {
            Progress = 30;
            StatusMessage = "Unpacking...";
            var (ok, output, count, error) = await _split.UnpackAsync(SourcePath, OutputPath, _busyCts.Token);
            if (ok)
            {
                Progress = 100;
                StatusMessage = $"Extracted {count} APK(s) to {output}";
                _activity.LogInfo($"Unpacked: {count} files", "UNP", "#22D3EE");
            }
            else StatusMessage = $"Unpack failed: {error}";
        }
        catch (OperationCanceledException) { StatusMessage = "Cancelled."; }
        catch (Exception ex) { StatusMessage = $"Unpack error: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            _busyCts?.Dispose();
            _busyCts = null;
        }
    }

    private async Task RunInstallAsync()
    {
        if (!HasValidSet)
        {
            if (string.IsNullOrEmpty(SourcePath)) return;
            await RunAnalyzeAsync();
            if (!HasValidSet)
            {
                StatusMessage = "Cannot install - no valid split set.";
                return;
            }
        }

        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first.";
            return;
        }

        IsBusy = true;
        Progress = 0;
        _busyCts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<string>(msg => StatusMessage = msg);
            var (ok, msgOut) = await _split.InstallToDeviceAsync(
                SelectedDevice.Serial, CurrentSet!, progress, _busyCts.Token);
            if (ok)
            {
                Progress = 100;
                StatusMessage = $"Installed to {SelectedDevice.DisplayName}";
                _activity.LogInstall($"Split install: {CurrentSet!.InferredName}");
            }
            else StatusMessage = $"Install failed: {msgOut}";
        }
        catch (OperationCanceledException) { StatusMessage = "Cancelled."; }
        catch (Exception ex) { StatusMessage = $"Install error: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            _busyCts?.Dispose();
            _busyCts = null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
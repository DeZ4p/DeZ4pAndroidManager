// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

/// <summary>
/// APK Installer - works on Android 7 → 16, all OEMs.
/// Handles .apk (single) and .apks (split archive).
/// </summary>
public class ApkInstallerViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> SplitExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".apks"
    };

    private readonly AdbService _adb;
    private readonly ActivityLogService _activity;

    private DeviceModel? _selectedDevice;
    private string _apkPath = string.Empty;
    private string _statusMessage = "Drag an APK or APKS file here, or click Browse.";
    private bool _isBusy;
    private bool _lastInstallSucceeded;

    public ApkInstallerViewModel(AdbService adb, ActivityLogService activity)
    {
        _adb = adb;
        _activity = activity;
        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
        InstallCommand = new AsyncRelayCommand(InstallAsync);
        BrowseCommand = new RelayCommand(_ => BrowseForApk());
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<string> InstallLog { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); }
    }

    public string ApkPath
    {
        get => _apkPath;
        set
        {
            _apkPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ApkFileName));
            OnPropertyChanged(nameof(HasApk));
        }
    }

    public string ApkFileName =>
        string.IsNullOrEmpty(ApkPath) ? "No file selected" : Path.GetFileName(ApkPath);

    public bool HasApk => !string.IsNullOrEmpty(ApkPath) && File.Exists(ApkPath);

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool LastInstallSucceeded
    {
        get => _lastInstallSucceeded;
        set { _lastInstallSucceeded = value; OnPropertyChanged(); }
    }

    public ICommand RefreshDevicesCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand BrowseCommand { get; }

    public void HandleDroppedFile(string path)
    {
        var ext = Path.GetExtension(path);
        bool isApk = ext.Equals(".apk", StringComparison.OrdinalIgnoreCase);
        bool isApks = SplitExtensions.Contains(ext);

        if (!isApk && !isApks)
        {
            StatusMessage = "Unsupported file. Use .apk or .apks.";
            return;
        }

        ApkPath = path;
        StatusMessage = isApks
            ? $"Loaded split package: {Path.GetFileName(path)}"
            : $"Loaded: {Path.GetFileName(path)}";
    }

    private void BrowseForApk()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Android package",
            Filter = "Android Package (*.apk;*.apks)|*.apk;*.apks|" +
                     "Single APK (*.apk)|*.apk|" +
                     "Split APK (*.apks)|*.apks|" +
                     "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
            HandleDroppedFile(dialog.FileName);
    }

    public async Task RefreshDevicesAsync()
    {
        if (!_adb.IsAdbAvailable) { StatusMessage = "adb not found."; return; }
        try
        {
            var list = await _adb.GetDevicesAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Devices.Clear();
                foreach (var d in list.Where(d => d.IsReady)) Devices.Add(d);
                if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];
            });
        }
        catch (Exception ex) { StatusMessage = $"Failed to scan: {ex.Message}"; }
    }

    private async Task InstallAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "Select a target device first."; return; }
        if (!HasApk) { StatusMessage = "Select a valid package first."; return; }

        IsBusy = true;
        InstallLog.Clear();
        LastInstallSucceeded = false;

        try
        {
            var ext = Path.GetExtension(ApkPath);
            if (SplitExtensions.Contains(ext)) await InstallSplitPackageAsync();
            else await InstallSingleApkAsync();
        }
        catch (Exception ex)
        {
            LastInstallSucceeded = false;
            StatusMessage = $"❌ Error: {ex.Message}";
            InstallLog.Add($"ERROR: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task InstallSingleApkAsync()
    {
        var fileName = Path.GetFileName(ApkPath);
        InstallLog.Add($"> adb -s {SelectedDevice!.Serial} install -r \"{fileName}\"");
        StatusMessage = "Installing...";

        var progress = new Progress<string>(line => InstallLog.Add(line));
        var result = await _adb.InstallApkAsync(SelectedDevice.Serial, ApkPath, progress);

        LastInstallSucceeded = result.Success;
        StatusMessage = result.Success ? "✅ Install succeeded." : "❌ Install failed.";
        if (result.Success) _activity.LogInstall($"Installed: {fileName}");
    }

    private async Task InstallSplitPackageAsync()
    {
        var packageName = Path.GetFileName(ApkPath);
        StatusMessage = "Extracting archive...";
        InstallLog.Add($"> Extracting: {packageName}  [.apks]");

        // Universal temp folder - works on Windows 10 and 11
        string tempBase = Path.GetTempPath();
        string tempDir = Path.Combine(tempBase, "DeZ4p_Split_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(ApkPath, tempDir);

            var apkFiles = Directory.GetFiles(tempDir, "*.apk", SearchOption.AllDirectories)
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

            if (apkFiles.Count == 0)
            {
                LastInstallSucceeded = false;
                StatusMessage = "❌ Archive contains no .apk files.";
                return;
            }

            InstallLog.Add($"Found {apkFiles.Count} APK file(s):");
            foreach (var f in apkFiles) InstallLog.Add($"   • {Path.GetFileName(f)}");

            InstallLog.Add($"> adb -s {SelectedDevice!.Serial} install-multiple -r <{apkFiles.Count} files>");
            StatusMessage = $"Installing {apkFiles.Count} split APKs...";

            var progress = new Progress<string>(line => InstallLog.Add(line));
            var result = await _adb.InstallSplitApkAsync(SelectedDevice.Serial, apkFiles, progress);

            LastInstallSucceeded = result.Success;
            StatusMessage = result.Success ? "✅ Install succeeded." : "❌ Install failed.";
            if (result.Success)
                _activity.LogInstall($"Installed: {packageName}  (split x{apkFiles.Count})");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
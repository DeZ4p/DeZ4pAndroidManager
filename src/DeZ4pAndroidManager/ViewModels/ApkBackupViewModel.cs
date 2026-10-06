// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class ApkBackupViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly AppsManagerService _apps;
    private readonly ApkBackupService _backup;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private string _currentSerial = string.Empty;
    private string _searchText = "";
    private bool _showSystemApps = false;
    private bool _isLoading;
    private bool _isBusy;
    private string _statusMessage = "";
    private string _outputFolder = "";
    private string _backupMode = "apk"; // apk | apks | apk+obb

    // Progress
    private double _overallProgress;
    private string _currentAppName = "";
    private int _doneCount;
    private int _totalCount;

    private CancellationTokenSource? _backupCts;

    public ApkBackupViewModel(AdbService adb, AppsManagerService apps, ApkBackupService backup,
                              DeviceStateService deviceState, ActivityLogService activity)
    {
        _adb = adb;
        _apps = apps;
        _backup = backup;
        _deviceState = deviceState;
        _activity = activity;

        FilteredApps = CollectionViewSource.GetDefaultView(Apps);
        FilteredApps.Filter = FilterApp;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        BackupSelectedCommand = new AsyncRelayCommand(BackupSelectedAsync);
        BackupAllCommand = new AsyncRelayCommand(BackupAllAsync);
        CancelCommand = new RelayCommand(_ => CancelBackup());
        SelectAllCommand = new RelayCommand(_ => SetSelectAll(true));
        DeselectAllCommand = new RelayCommand(_ => SetSelectAll(false));
        OpenFolderCommand = new RelayCommand(_ => OpenOutputFolder());
        ClearHistoryCommand = new RelayCommand(_ => History.Clear());
        OpenHistoryFolderCommand = new RelayCommand(param => OpenHistoryFolder(param as BackupItem));
        CopyToDeviceCommand = new AsyncRelayCommand(CopySelectedToDeviceAsync);
        SetModeCommand = new RelayCommand(p => BackupMode = p as string ?? "apk");

        // Central output folder under Documents\DeZ4p Android Manager\APK Backups
        OutputFolder = AppPaths.ApkBackups;

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try { await OnDeviceChangedAsync(); } catch { }
            });
        };

        _ = OnDeviceChangedAsync();
    }

    public ObservableCollection<AppItem> Apps { get; } = new();
    public ObservableCollection<AppItem> SelectedApps { get; } = new();
    public ObservableCollection<BackupItem> History { get; } = new();
    public ICollectionView FilteredApps { get; }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; OnPropertyChanged(); FilteredApps.Refresh(); }
    }

    public bool ShowSystemApps
    {
        get => _showSystemApps;
        set { _showSystemApps = value; OnPropertyChanged(); FilteredApps.Refresh(); }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set { _outputFolder = value ?? ""; OnPropertyChanged(); }
    }

    public string BackupMode
    {
        get => _backupMode;
        set
        {
            _backupMode = value ?? "apk";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsModeApk));
            OnPropertyChanged(nameof(IsModeApks));
            OnPropertyChanged(nameof(IsModeObb));
        }
    }

    public bool IsModeApk => BackupMode == "apk";
    public bool IsModeApks => BackupMode == "apks";
    public bool IsModeObb => BackupMode == "apk+obb";

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

    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); }
    }

    public string CurrentAppName
    {
        get => _currentAppName;
        set { _currentAppName = value ?? ""; OnPropertyChanged(); }
    }

    public int DoneCount
    {
        get => _doneCount;
        set { _doneCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountDisplay)); }
    }

    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountDisplay)); }
    }

    public string CountDisplay => TotalCount <= 0 ? "" : $"{DoneCount} / {TotalCount}";

    public bool HasDevice => !string.IsNullOrEmpty(_currentSerial);
    public int SelectedCount => SelectedApps.Count;
    public bool HasSelection => SelectedApps.Count > 0;

    public ICommand RefreshCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand BackupSelectedCommand { get; }
    public ICommand BackupAllCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand OpenHistoryFolderCommand { get; }
    public ICommand CopyToDeviceCommand { get; }
    public ICommand SetModeCommand { get; }

    public void UpdateSelection(IList<AppItem> items)
    {
        SelectedApps.Clear();
        foreach (var i in items) SelectedApps.Add(i);
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void SetSelectAll(bool selected)
    {
        // AppItem is a reference from list; selection sync happens in View via ListView.SelectedItems
        // Here we just notify
        OnPropertyChanged(nameof(SelectedCount));
    }

    private bool FilterApp(object o)
    {
        if (o is not AppItem a) return false;
        if (!ShowSystemApps && a.IsSystem) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var q = SearchText.Trim();
        return a.PackageName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || a.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void BrowseFolder()
    {
        try
        {
            var ofd = new Microsoft.Win32.OpenFolderDialog { Title = "Choose backup folder" };
            if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
                ofd.InitialDirectory = OutputFolder;
            if (ofd.ShowDialog() == true)
                OutputFolder = ofd.FolderName;
        }
        catch { }
    }

    private void OpenOutputFolder()
    {
        try
        {
            if (string.IsNullOrEmpty(OutputFolder)) return;
            Directory.CreateDirectory(OutputFolder);
            Process.Start(new ProcessStartInfo { FileName = OutputFolder, UseShellExecute = true });
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
    }

    private void OpenHistoryFolder(BackupItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.LocalPath)) return;
        try
        {
            var path = item.LocalPath;
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch { }
    }

    private void CancelBackup()
    {
        try { _backupCts?.Cancel(); } catch { }
    }

    private async Task OnDeviceChangedAsync()
    {
        var d = _deviceState.CurrentDevice;
        if (d == null || d.IsFastboot)
        {
            _currentSerial = "";
            Apps.Clear();
            SelectedApps.Clear();
            StatusMessage = d == null
                ? LocalizationService.Translate("AppsManager.NoDevice")
                : LocalizationService.Translate("AppsManager.NeedAdb");
            OnPropertyChanged(nameof(HasDevice));
            return;
        }

        _currentSerial = d.Serial;
        OnPropertyChanged(nameof(HasDevice));
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (!HasDevice) return;
        IsLoading = true;
        try
        {
            var list = await _apps.ListAppsAsync(_currentSerial);
            Apps.Clear();
            foreach (var a in list) Apps.Add(a);
            FilteredApps.Refresh();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private async Task BackupSelectedAsync()
    {
        if (SelectedApps.Count == 0)
        {
            StatusMessage = "Select at least one app.";
            return;
        }
        await RunBackupAsync(SelectedApps.ToList());
    }

    private async Task BackupAllAsync()
    {
        var all = FilteredApps.Cast<AppItem>().ToList();
        if (all.Count == 0) return;

        var confirm = MessageBox.Show(
            $"Backup all {all.Count} apps?&#10;&#10;This may take a while.".Replace("&#10;", "\n"),
            "Confirm Backup All",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await RunBackupAsync(all);
    }

    private async Task RunBackupAsync(List<AppItem> targets)
    {
        if (!HasDevice) return;
        if (string.IsNullOrEmpty(OutputFolder))
        {
            StatusMessage = "Choose output folder first.";
            return;
        }

        try { Directory.CreateDirectory(OutputFolder); }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; return; }

        IsBusy = true;
        TotalCount = targets.Count;
        DoneCount = 0;
        OverallProgress = 0;
        _backupCts = new CancellationTokenSource();
        var token = _backupCts.Token;

        var okCount = 0;
        var failCount = 0;

        try
        {
            foreach (var app in targets)
            {
                token.ThrowIfCancellationRequested();

                CurrentAppName = app.DisplayName;
                var item = new BackupItem
                {
                    PackageName = app.PackageName,
                    DisplayName = app.DisplayName,
                    VersionName = app.VersionName,
                    IsSystem = app.IsSystem,
                    Status = BackupStatus.Running,
                    StartedAt = DateTime.Now
                };
                History.Insert(0, item);

                try
                {
                    var paths = await _backup.GetApkPathsAsync(_currentSerial, app.PackageName, token);
                    item.ApkCount = paths.Count;

                    if (paths.Count == 0)
                    {
                        item.Status = BackupStatus.Failed;
                        item.ErrorMessage = "No APK paths";
                        failCount++;
                        continue;
                    }

                    // Show estimated size
                    try { item.TotalBytes = await _backup.GetApkSizeAsync(_currentSerial, paths, token); }
                    catch { }

                    var progress = new Progress<(int done, int total)>(p =>
                    {
                        item.ApkDone = p.done;
                        item.Progress = p.total > 0 ? (double)p.done / p.total * 100 : 0;
                    });

                    if (BackupMode == "apks")
                    {
                        var (ok, file) = await _backup.PullAsApksBundleAsync(
                            _currentSerial, app.PackageName, app.DisplayName, OutputFolder, progress, token);
                        if (ok)
                        {
                            item.LocalPath = file;
                            try { item.TotalBytes = new FileInfo(file).Length; } catch { }
                            item.Progress = 100;
                            item.Status = BackupStatus.Done;
                            item.FinishedAt = DateTime.Now;
                            okCount++;
                            _activity.LogBackup($"Backed up: {app.DisplayName} (bundle)");
                        }
                        else
                        {
                            item.Status = BackupStatus.Failed;
                            item.ErrorMessage = "Bundle failed";
                            failCount++;
                        }
                    }
                    else
                    {
                        var (pulled, bytes, folder) = await _backup.PullApksAsync(
                            _currentSerial, app.PackageName, app.DisplayName, OutputFolder, progress, token);

                        if (pulled > 0)
                        {
                            item.TotalBytes = bytes > 0 ? bytes : item.TotalBytes;
                            item.LocalPath = folder;
                            item.Progress = 100;
                            item.Status = BackupStatus.Done;
                            item.FinishedAt = DateTime.Now;
                            okCount++;
                            _activity.LogBackup($"Backed up: {app.DisplayName}");
                        }
                        else
                        {
                            item.Status = BackupStatus.Failed;
                            item.ErrorMessage = "Pull failed";
                            failCount++;
                        }
                    }

                    // OBB (optional)
                    if (BackupMode == "apk+obb" && item.Status == BackupStatus.Done)
                    {
                        try
                        {
                            var (obbCount, obbBytes, obbFolder) = await _backup.PullObbAsync(
                                _currentSerial, app.PackageName, OutputFolder, token);
                            if (obbCount > 0)
                            {
                                item.TotalBytes += obbBytes;
                                item.LocalPath = obbFolder; // show obb folder
                                _activity.LogBackup($"OBB pulled: {app.DisplayName} ({obbCount} files)");
                            }
                        }
                        catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    item.Status = BackupStatus.Cancelled;
                    throw;
                }
                catch (Exception ex)
                {
                    item.Status = BackupStatus.Failed;
                    item.ErrorMessage = ex.Message;
                    failCount++;
                }
                finally
                {
                    DoneCount++;
                    OverallProgress = (double)DoneCount / TotalCount * 100;
                }
            }

            StatusMessage = $"✅ Done - {okCount} OK, {failCount} failed";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "⏹ Backup cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CurrentAppName = "";
            try { _backupCts?.Dispose(); } catch { }
            _backupCts = null;
        }
    }

    private async Task CopySelectedToDeviceAsync()
    {
        if (SelectedApps.Count == 0) { StatusMessage = "Select app(s) first."; return; }
        if (!HasDevice) return;

        IsBusy = true;
        try
        {
            const string dest = "/sdcard/DeZ4p_Backups";
            int ok = 0;
            foreach (var app in SelectedApps)
            {
                StatusMessage = $"📱 {app.DisplayName}";
                if (await _backup.CopyApksToDeviceFolderAsync(_currentSerial, app.PackageName, dest))
                    ok++;
            }
            StatusMessage = $"✅ Copied to {dest} - {ok}/{SelectedApps.Count}";
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
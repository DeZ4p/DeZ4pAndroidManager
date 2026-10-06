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
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class BackupRestoreViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly BackupRestoreService _backup;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private string _currentSerial = "";
    private string _deviceName = "";
    private string _backupFolder = "";
    private string _restoreFolder = "";
    private string _tab = "backup";   // backup | restore
    private bool _isBusy;
    private double _overallProgress;
    private string _statusMessage = "Ready.";
    private string _currentNote = "";
    private DeviceModel? _selectedDevice;
    private BackupManifest? _restoreManifest;

    private CancellationTokenSource? _busyCts;

    public BackupRestoreViewModel(AdbService adb, BackupRestoreService backup,
                                  DeviceStateService deviceState, DeviceWatcherService watcher,
                                  ActivityLogService activity)
    {
        _adb = adb;
        _backup = backup;
        _deviceState = deviceState;
        _activity = activity;

        // Central output folder under Documents\DeZ4p Android Manager\Device Backups
        BackupFolder = AppPaths.DeviceBackups;

        // Init categories
        BackupCategories = new ObservableCollection<BackupCategory>
        {
            new() { Kind = BackupCategoryKind.Contacts, Title = "Contacts", Description = "All saved contacts (vCard format)", Icon = "\uE77B", Color = "#4F8CFF", IsSelected = true },
            new() { Kind = BackupCategoryKind.Sms, Title = "SMS Messages", Description = "All text messages", Icon = "\uE8BD", Color = "#22D3EE", IsSelected = true },
            new() { Kind = BackupCategoryKind.CallLogs, Title = "Call Logs", Description = "Incoming, outgoing and missed calls", Icon = "\uE717", Color = "#34D399", IsSelected = true },
            new() { Kind = BackupCategoryKind.Media, Title = "Media Files", Description = "Photos, videos, music, downloads", Icon = "\uEB9F", Color = "#EC4899", IsSelected = false },
            new() { Kind = BackupCategoryKind.AppsList, Title = "Installed Apps List", Description = "List of user-installed package names", Icon = "\uE71D", Color = "#A855F7", IsSelected = true },
            new() { Kind = BackupCategoryKind.WhatsApp, Title = "WhatsApp Folder", Description = "Chats, media and databases", Icon = "\uE8D6", Color = "#25D366", IsSelected = false }
        };

        RestoreCategories = new ObservableCollection<BackupCategory>();

        SetTabCommand = new RelayCommand(p => Tab = p as string ?? "backup");
        BrowseBackupFolderCommand = new RelayCommand(_ => BrowseBackupFolder());
        BrowseRestoreFolderCommand = new RelayCommand(_ => BrowseRestoreFolder());
        StartBackupCommand = new AsyncRelayCommand(StartBackupAsync);
        StartRestoreCommand = new AsyncRelayCommand(StartRestoreAsync);
        CancelCommand = new RelayCommand(_ => CancelBusy());
        OpenBackupFolderCommand = new RelayCommand(_ => OpenFolder(BackupFolder));
        OpenRestoreFolderCommand = new RelayCommand(_ => OpenFolder(RestoreFolder));
        OpenHistoryItemCommand = new RelayCommand(p => OpenFolder(p as string));
        ClearHistoryCommand = new RelayCommand(_ => History.Clear());
        RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);

        RefreshDevices();
    }

    public ObservableCollection<BackupCategory> BackupCategories { get; }
    public ObservableCollection<BackupCategory> RestoreCategories { get; }
    public ObservableCollection<BackupHistoryItem> History { get; } = new();
    public ObservableCollection<DeviceModel> Devices { get; } = new();

    // ═══ TABS ═══
    public string Tab
    {
        get => _tab;
        set
        {
            _tab = value ?? "backup";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBackupTab));
            OnPropertyChanged(nameof(IsRestoreTab));
        }
    }

    public bool IsBackupTab => Tab == "backup";
    public bool IsRestoreTab => Tab == "restore";

    // ═══ FOLDERS ═══
    public string BackupFolder
    {
        get => _backupFolder;
        set { _backupFolder = value ?? ""; OnPropertyChanged(); }
    }

    public string RestoreFolder
    {
        get => _restoreFolder;
        set
        {
            _restoreFolder = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasRestoreFolder));
            _ = LoadRestoreManifestAsync();
        }
    }

    public bool HasRestoreFolder => !string.IsNullOrEmpty(_restoreFolder);

    // ═══ STATE ═══
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public double OverallProgress
    {
        get => _overallProgress;
        set { _overallProgress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public string CurrentNote
    {
        get => _currentNote;
        set { _currentNote = value ?? ""; OnPropertyChanged(); }
    }

    public string DeviceName
    {
        get => _deviceName;
        set { _deviceName = value ?? ""; OnPropertyChanged(); }
    }

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }

    public bool HasDevice => _selectedDevice != null;

    public bool HasManifest => _restoreManifest != null;

    public string ManifestInfo => _restoreManifest == null
        ? ""
        : $"{_restoreManifest.DeviceName} - Android {_restoreManifest.AndroidVersion} - {_restoreManifest.CreatedDisplay}";

    // ═══ COMMANDS ═══
    public ICommand SetTabCommand { get; }
    public ICommand BrowseBackupFolderCommand { get; }
    public ICommand BrowseRestoreFolderCommand { get; }
    public ICommand StartBackupCommand { get; }
    public ICommand StartRestoreCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenBackupFolderCommand { get; }
    public ICommand OpenRestoreFolderCommand { get; }
    public ICommand OpenHistoryItemCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RefreshDevicesCommand { get; }

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
        if (!string.IsNullOrEmpty(cur)) SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];

        _currentSerial = SelectedDevice?.Serial ?? "";
        DeviceName = SelectedDevice?.DisplayName ?? "";
    }

    private void BrowseBackupFolder()
    {
        var ofd = new OpenFolderDialog { Title = "Choose backup destination" };
        if (ofd.ShowDialog() == true) BackupFolder = ofd.FolderName;
    }

    private void BrowseRestoreFolder()
    {
        var ofd = new OpenFolderDialog { Title = "Choose backup folder to restore from" };
        if (ofd.ShowDialog() == true) RestoreFolder = ofd.FolderName;
    }

    private void OpenFolder(string? folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        try
        {
            if (Directory.Exists(folder))
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch { }
    }

    private void CancelBusy()
    {
        try { _busyCts?.Cancel(); } catch { }
        StatusMessage = "Cancel requested...";
    }

    // ═══════════════════════════════════════════════════════════
    //  BACKUP
    // ═══════════════════════════════════════════════════════════
    private async Task StartBackupAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Select a device first.";
            return;
        }
        var selected = BackupCategories.Where(c => c.IsSelected).Select(c => c.Kind).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Choose at least one category.";
            return;
        }
        if (string.IsNullOrWhiteSpace(BackupFolder))
        {
            StatusMessage = "Choose a backup folder.";
            return;
        }

        try { Directory.CreateDirectory(BackupFolder); }
        catch (Exception ex) { StatusMessage = $"Cannot create folder: {ex.Message}"; return; }

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var targetFolder = Path.Combine(BackupFolder, $"Backup_{stamp}");
        try { Directory.CreateDirectory(targetFolder); } catch (Exception ex) { StatusMessage = $"Cannot create: {ex.Message}"; return; }

        IsBusy = true;
        OverallProgress = 0;
        _busyCts = new CancellationTokenSource();
        var token = _busyCts.Token;

        // Reset per-category stats
        foreach (var c in BackupCategories)
        {
            c.Progress = 0;
            c.ItemCount = 0;
            c.Bytes = 0;
            c.StatusNote = "";
        }

        var progress = new Progress<(BackupCategoryKind kind, double percent, string note)>(p =>
        {
            var cat = BackupCategories.FirstOrDefault(c => c.Kind == p.kind);
            if (cat != null)
            {
                cat.Progress = p.percent;
                cat.StatusNote = p.note;
            }
            // Compute overall = average of selected
            var selectedCats = BackupCategories.Where(c => c.IsSelected).ToList();
            if (selectedCats.Count > 0)
                OverallProgress = selectedCats.Average(c => c.Progress);
            CurrentNote = $"{p.kind}: {p.note}";
        });

        try
        {
            StatusMessage = $"Backing up to {Path.GetFileName(targetFolder)}...";

            var manifest = await _backup.CreateBackupAsync(
                SelectedDevice.Serial, targetFolder, selected, progress, token);

            // Update per-category stats from manifest
            foreach (var cat in BackupCategories)
            {
                var key = CategoryKey(cat.Kind);
                if (manifest.Categories.TryGetValue(key, out var info))
                {
                    cat.ItemCount = info.Count;
                    cat.Bytes = info.Bytes;
                    cat.Progress = 100;
                }
            }

            OverallProgress = 100;
            StatusMessage = $"Backup complete - {manifest.TotalItems} items, {manifest.SizeDisplay}";

            History.Insert(0, new BackupHistoryItem
            {
                FolderPath = targetFolder,
                Name = Path.GetFileName(targetFolder),
                CreatedAt = DateTime.Now,
                DeviceName = manifest.DeviceName,
                TotalItems = manifest.TotalItems,
                TotalBytes = manifest.TotalBytes
            });

            _activity.LogBackup($"Backup: {manifest.TotalItems} items ({manifest.SizeDisplay})");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Backup cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _busyCts?.Dispose();
            _busyCts = null;
        }
    }

    private static string CategoryKey(BackupCategoryKind k) => k switch
    {
        BackupCategoryKind.Contacts => "contacts",
        BackupCategoryKind.Sms => "sms",
        BackupCategoryKind.CallLogs => "callLogs",
        BackupCategoryKind.Media => "media",
        BackupCategoryKind.AppsList => "apps",
        BackupCategoryKind.WhatsApp => "whatsapp",
        _ => ""
    };

    // ═══════════════════════════════════════════════════════════
    //  RESTORE
    // ═══════════════════════════════════════════════════════════
    private async Task LoadRestoreManifestAsync()
    {
        if (string.IsNullOrEmpty(RestoreFolder))
        {
            _restoreManifest = null;
            RestoreCategories.Clear();
            OnPropertyChanged(nameof(HasManifest));
            OnPropertyChanged(nameof(ManifestInfo));
            return;
        }

        var manifest = await Task.Run(() => BackupManifest.Load(RestoreFolder));
        _restoreManifest = manifest;
        RestoreCategories.Clear();

        if (manifest != null)
        {
            foreach (var kv in manifest.Categories)
            {
                var cat = new BackupCategory
                {
                    Kind = KeyToKind(kv.Key),
                    Title = KindTitle(kv.Key),
                    Description = $"{kv.Value.Count} items - {MediaItem.FormatSize(kv.Value.Bytes)}",
                    Icon = KindIcon(kv.Key),
                    Color = KindColor(kv.Key),
                    ItemCount = kv.Value.Count,
                    Bytes = kv.Value.Bytes,
                    IsSelected = true
                };
                RestoreCategories.Add(cat);
            }
            StatusMessage = $"Loaded manifest - {manifest.TotalItems} items, {manifest.SizeDisplay}";
        }
        else
        {
            StatusMessage = "No valid backup found in that folder (manifest.json missing).";
        }

        OnPropertyChanged(nameof(HasManifest));
        OnPropertyChanged(nameof(ManifestInfo));
    }

    private static BackupCategoryKind KeyToKind(string key) => key switch
    {
        "contacts" => BackupCategoryKind.Contacts,
        "sms" => BackupCategoryKind.Sms,
        "callLogs" => BackupCategoryKind.CallLogs,
        "media" => BackupCategoryKind.Media,
        "apps" => BackupCategoryKind.AppsList,
        "whatsapp" => BackupCategoryKind.WhatsApp,
        _ => BackupCategoryKind.Contacts
    };

    private static string KindTitle(string key) => key switch
    {
        "contacts" => "Contacts",
        "sms" => "SMS Messages",
        "callLogs" => "Call Logs",
        "media" => "Media Files",
        "apps" => "Installed Apps List",
        "whatsapp" => "WhatsApp Folder",
        _ => key
    };

    private static string KindIcon(string key) => key switch
    {
        "contacts" => "\uE77B",
        "sms" => "\uE8BD",
        "callLogs" => "\uE717",
        "media" => "\uEB9F",
        "apps" => "\uE71D",
        "whatsapp" => "\uE8D6",
        _ => "\uE8B7"
    };

    private static string KindColor(string key) => key switch
    {
        "contacts" => "#4F8CFF",
        "sms" => "#22D3EE",
        "callLogs" => "#34D399",
        "media" => "#EC4899",
        "apps" => "#A855F7",
        "whatsapp" => "#25D366",
        _ => "#4F8CFF"
    };

    private async Task StartRestoreAsync()
    {
        if (SelectedDevice == null) { StatusMessage = "Select a device first."; return; }
        if (_restoreManifest == null) { StatusMessage = "Load a backup first."; return; }

        var selected = RestoreCategories.Where(c => c.IsSelected).Select(c => c.Kind).ToList();
        if (selected.Count == 0) { StatusMessage = "Choose at least one category."; return; }

        var confirm = MessageBox.Show(
            $"Restore {selected.Count} categories to {SelectedDevice.DisplayName}?\n\n" +
            "This may take a while and some categories (SMS, Call Logs) may produce duplicates.",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        OverallProgress = 0;
        _busyCts = new CancellationTokenSource();
        var token = _busyCts.Token;

        foreach (var c in RestoreCategories) { c.Progress = 0; c.StatusNote = ""; }

        var progress = new Progress<(BackupCategoryKind kind, double percent, string note)>(p =>
        {
            var cat = RestoreCategories.FirstOrDefault(c => c.Kind == p.kind);
            if (cat != null) { cat.Progress = p.percent; cat.StatusNote = p.note; }

            var sel = RestoreCategories.Where(c => c.IsSelected).ToList();
            if (sel.Count > 0) OverallProgress = sel.Average(c => c.Progress);
            CurrentNote = $"{p.kind}: {p.note}";
        });

        try
        {
            StatusMessage = "Restoring...";
            var (ok, fail) = await _backup.RestoreAsync(
                SelectedDevice.Serial, RestoreFolder, _restoreManifest, selected, progress, token);

            OverallProgress = 100;
            StatusMessage = $"Restore complete - {ok} ok, {fail} failed";
            _activity.LogInfo($"Restore: {ok} categories ok", "RST", "#34D399");
        }
        catch (OperationCanceledException) { StatusMessage = "Restore cancelled."; }
        catch (Exception ex) { StatusMessage = $"Restore failed: {ex.Message}"; }
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

public class BackupHistoryItem
{
    public string FolderPath { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string DeviceName { get; set; } = "";
    public int TotalItems { get; set; }
    public long TotalBytes { get; set; }

    public string SizeDisplay => MediaItem.FormatSize(TotalBytes);
    public string CreatedDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm");
    public string InfoDisplay => $"{TotalItems} items - {SizeDisplay}";
}
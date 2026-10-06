// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class AppsManagerViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly AppsManagerService _apps;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private string _currentSerial = string.Empty;
    private string _searchText = "";
    private bool _showSystemApps = false;
    private bool _isBusy;
    private bool _isLoading;
    private string _statusMessage = "";
    private AppItem? _selectedItem;

    public AppsManagerViewModel(AdbService adb, AppsManagerService apps,
                                 DeviceStateService deviceState,
                                 ActivityLogService activity)
    {
        _adb = adb;
        _apps = apps;
        _deviceState = deviceState;
        _activity = activity;

        FilteredApps = CollectionViewSource.GetDefaultView(Apps);
        FilteredApps.Filter = FilterApp;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LaunchCommand = new AsyncRelayCommand(LaunchSelectedAsync);
        ForceStopCommand = new AsyncRelayCommand(ForceStopSelectedAsync);
        ClearDataCommand = new AsyncRelayCommand(ClearDataSelectedAsync);
        EnableCommand = new AsyncRelayCommand(EnableSelectedAsync);
        DisableCommand = new AsyncRelayCommand(DisableSelectedAsync);
        UninstallCommand = new AsyncRelayCommand(UninstallSelectedAsync);
        ExportApkCommand = new AsyncRelayCommand(ExportSelectedApkAsync);
        CopyPackageCommand = new RelayCommand(_ => CopyPackageName());

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
    public ObservableCollection<AppItem> SelectedItems { get; } = new();
    public ICollectionView FilteredApps { get; }

    public AppItem? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; OnPropertyChanged(); FilteredApps.Refresh(); UpdateStatusCount(); }
    }

    public bool ShowSystemApps
    {
        get => _showSystemApps;
        set { _showSystemApps = value; OnPropertyChanged(); FilteredApps.Refresh(); UpdateStatusCount(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public bool HasDevice => !string.IsNullOrEmpty(_currentSerial);
    public bool HasSelection => SelectedItems.Count > 0;
    public int SelectionCount => SelectedItems.Count;

    public ICommand RefreshCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand ForceStopCommand { get; }
    public ICommand ClearDataCommand { get; }
    public ICommand EnableCommand { get; }
    public ICommand DisableCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand ExportApkCommand { get; }
    public ICommand CopyPackageCommand { get; }

    // ═══════════════════════════════════════════════════════
    //  SELECTION
    // ═══════════════════════════════════════════════════════
    public void UpdateSelection(IList<AppItem> items)
    {
        SelectedItems.Clear();
        foreach (var i in items) SelectedItems.Add(i);
        SelectedItem = SelectedItems.FirstOrDefault();

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCount));

        if (SelectedItems.Count == 0) UpdateStatusCount();
        else StatusMessage = $"{SelectedItems.Count} {LocalizationService.Translate("AppsManager.ItemsSelected")}";
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

    private void UpdateStatusCount()
    {
        var visible = FilteredApps.Cast<object>().Count();
        StatusMessage = visible == 0
            ? LocalizationService.Translate("AppsManager.EmptyList")
            : $"{visible} {LocalizationService.Translate("AppsManager.AppsCount")}";
    }

    // ═══════════════════════════════════════════════════════
    //  DEVICE STATE
    // ═══════════════════════════════════════════════════════
    private async Task OnDeviceChangedAsync()
    {
        var device = _deviceState.CurrentDevice;

        if (device == null || device.IsFastboot)
        {
            _currentSerial = "";
            Apps.Clear();
            SelectedItems.Clear();
            StatusMessage = device == null
                ? LocalizationService.Translate("AppsManager.NoDevice")
                : LocalizationService.Translate("AppsManager.NeedAdb");
            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(HasSelection));
            return;
        }

        _currentSerial = device.Serial;
        OnPropertyChanged(nameof(HasDevice));

        if (Apps.Count == 0)
            await RefreshAsync();
    }

    // ═══════════════════════════════════════════════════════
    //  REFRESH
    // ═══════════════════════════════════════════════════════
    public async Task RefreshAsync()
    {
        if (!HasDevice) return;

        IsLoading = true;
        try
        {
            var list = await _apps.ListAppsAsync(_currentSerial);
            Apps.Clear();
            foreach (var a in list) Apps.Add(a);

            SelectedItems.Clear();
            SelectedItem = null;
            FilteredApps.Refresh();
            UpdateStatusCount();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // ═══════════════════════════════════════════════════════
    //  ACTIONS
    // ═══════════════════════════════════════════════════════
    private async Task LaunchSelectedAsync()
    {
        if (SelectedItem == null) return;
        if (!SelectedItem.IsEnabled)
        {
            StatusMessage = LocalizationService.Translate("AppsManager.AppDisabled");
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await _apps.LaunchAsync(_currentSerial, SelectedItem.PackageName);
            StatusMessage = ok
                ? $"✅ {LocalizationService.Translate("AppsManager.Launched")}"
                : $"❌ {LocalizationService.Translate("AppsManager.LaunchFailed")}";
        }
        finally { IsBusy = false; }
    }

    private async Task ForceStopSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in SelectedItems.ToList())
            {
                if (await _apps.ForceStopAsync(_currentSerial, item.PackageName)) ok++;
            }
            StatusMessage = $"✅ {LocalizationService.Translate("AppsManager.ForceStopped")} ({ok})";
        }
        finally { IsBusy = false; }
    }

    private async Task ClearDataSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        var confirm = MessageBox.Show(
            string.Format(LocalizationService.Translate("AppsManager.ConfirmClearData"),
                          SelectedItems.Count == 1 ? SelectedItems[0].DisplayName : $"{SelectedItems.Count} apps"),
            LocalizationService.Translate("AppsManager.Confirm"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in SelectedItems.ToList())
            {
                if (await _apps.ClearDataAsync(_currentSerial, item.PackageName)) ok++;
            }
            StatusMessage = ok == SelectedItems.Count
                ? $"✅ {LocalizationService.Translate("AppsManager.Cleared")} ({ok})"
                : $"⚠ {ok}/{SelectedItems.Count} {LocalizationService.Translate("AppsManager.Cleared")}";
        }
        finally { IsBusy = false; }
    }

    private async Task EnableSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in SelectedItems.ToList())
            {
                if (await _apps.EnableAsync(_currentSerial, item.PackageName)) ok++;
            }
            await RefreshAsync();
            StatusMessage = $"✅ {LocalizationService.Translate("AppsManager.Enabled")} ({ok})";
        }
        finally { IsBusy = false; }
    }

    private async Task DisableSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        // Warning for system apps
        var systemCount = SelectedItems.Count(i => i.IsSystem);
        if (systemCount > 0)
        {
            var warn = MessageBox.Show(
                string.Format(LocalizationService.Translate("AppsManager.ConfirmDisableSystem"), systemCount),
                LocalizationService.Translate("AppsManager.Confirm"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (warn != MessageBoxResult.Yes) return;
        }

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in SelectedItems.ToList())
            {
                if (await _apps.DisableAsync(_currentSerial, item.PackageName)) ok++;
            }
            await RefreshAsync();
            StatusMessage = $"✅ {LocalizationService.Translate("AppsManager.Disabled")} ({ok})";
        }
        finally { IsBusy = false; }
    }

    private async Task UninstallSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        var item = SelectedItems[0];
        var msg = SelectedItems.Count == 1
            ? string.Format(LocalizationService.Translate("AppsManager.ConfirmUninstallMsg"), item.DisplayName)
            : string.Format(LocalizationService.Translate("AppsManager.ConfirmUninstallMsg"), $"{SelectedItems.Count} apps");

        var result = MessageBox.Show(
            msg,
            LocalizationService.Translate("AppsManager.ConfirmUninstallTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel) return;

        bool keepData = result == MessageBoxResult.Yes;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var a in SelectedItems.ToList())
            {
                if (await _apps.UninstallAsync(_currentSerial, a.PackageName, keepData)) ok++;
            }
            await RefreshAsync();
            StatusMessage = ok == SelectedItems.Count
                ? $"✅ {LocalizationService.Translate("AppsManager.Uninstalled")} ({ok})"
                : $"⚠ {ok}/{SelectedItems.Count}";
        }
        finally { IsBusy = false; }
    }

    private async Task ExportSelectedApkAsync()
    {
        if (SelectedItem == null) return;

        var pkg = SelectedItem.PackageName;
        IsBusy = true;
        try
        {
            var paths = await _apps.GetApkPathsAsync(_currentSerial, pkg);
            if (paths.Count == 0)
            {
                StatusMessage = $"❌ {LocalizationService.Translate("AppsManager.ExportFailed")}";
                return;
            }

            if (paths.Count == 1)
            {
                var sfd = new SaveFileDialog
                {
                    FileName = pkg + ".apk",
                    Filter = "APK files (*.apk)|*.apk|All files (*.*)|*.*",
                    Title = LocalizationService.Translate("AppsManager.ExportApkTitle")
                };
                if (sfd.ShowDialog() != true) return;

                var ok = await _apps.ExportApkAsync(_currentSerial, pkg, sfd.FileName, toFolder: false);
                StatusMessage = ok
                    ? $"✅ {string.Format(LocalizationService.Translate("AppsManager.Exported"), sfd.FileName)}"
                    : $"❌ {LocalizationService.Translate("AppsManager.ExportFailed")}";
            }
            else
            {
                var ofd = new OpenFolderDialog { Title = LocalizationService.Translate("AppsManager.ExportFolderTitle") };
                if (ofd.ShowDialog() != true) return;

                var folder = Path.Combine(ofd.FolderName, pkg);
                var ok = await _apps.ExportApkAsync(_currentSerial, pkg, folder, toFolder: true);
                StatusMessage = ok
                    ? $"✅ {string.Format(LocalizationService.Translate("AppsManager.Exported"), folder)}"
                    : $"❌ {LocalizationService.Translate("AppsManager.ExportFailed")}";
            }
        }
        finally { IsBusy = false; }
    }

    private void CopyPackageName()
    {
        if (SelectedItems.Count == 0) return;
        var txt = string.Join(Environment.NewLine, SelectedItems.Select(i => i.PackageName));
        try
        {
            System.Windows.Clipboard.SetText(txt);
            StatusMessage = $"📋 {LocalizationService.Translate("AppsManager.PackageCopied")}";
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
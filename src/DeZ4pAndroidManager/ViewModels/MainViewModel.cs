// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DeZ4pAndroidManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DeviceStateService _deviceState;
    private readonly DeviceWatcherService _watcher;

    private NavItem? _selectedNavItem;
    private object? _currentView;
    private bool _showConnectedFlash;
    private DispatcherTimer? _flashTimer;
    private string _flashMessage = "Device Connected";

    // Access groups
    private static readonly string[] FreeAlways = { "adb", "recovery", "fastboot", "fastbootd", "unauthorized" };
    private static readonly string[] AdbOnly = { "adb" };
    private static readonly string[] AdbOrRecovery = { "adb", "recovery" };
    private static readonly string[] FastbootOrFastbootd = { "fastboot", "fastbootd" };
    private static readonly string[] FastbootdOnly = { "fastbootd" };

    public MainViewModel()
    {
        _deviceState = App.Services.GetRequiredService<DeviceStateService>();
        _watcher = App.Services.GetRequiredService<DeviceWatcherService>();

        NavItems = new ObservableCollection<NavItem>(BuildNavItems());
        SelectedNavItem = NavItems.FirstOrDefault();

        LocalizationService.LanguageChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                string? currentTitleKey = _selectedNavItem?.TitleKey;
                NavItems.Clear();
                foreach (var item in BuildNavItems()) NavItems.Add(item);

                var target = NavItems.FirstOrDefault(n => n.TitleKey == currentTitleKey) ?? NavItems.FirstOrDefault();
                if (target != null)
                {
                    _selectedNavItem = target;
                    OnPropertyChanged(nameof(SelectedNavItem));
                    CurrentView = target.ViewModel;
                }

                UpdateLockStates();
                OnPropertyChanged(nameof(IsRtl));
                RaiseAccessChanged();
            });
        };

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateLockStates();
                OnPropertyChanged(nameof(IsDeviceConnected));
                RaiseAccessChanged();

                // If current page is now blocked → auto-navigate to a safe menu
                if (_selectedNavItem != null && IsBlockedByMode(_selectedNavItem))
                {
                    var safe = FindSafeMenuItem();
                    if (safe != null && safe != _selectedNavItem)
                    {
                        _selectedNavItem = safe;
                        OnPropertyChanged(nameof(SelectedNavItem));
                        CurrentView = safe.ViewModel;
                    }
                }
            });
        };

        _watcher.DeviceConnected += (_, device) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
                ShowFlash($"{LocalizationService.Translate("Toast.Connected")}  -  {device.DisplayName}"));
        };

        UpdateLockStates();
    }

    public ObservableCollection<NavItem> NavItems { get; }

    public bool IsRtl => LocalizationService.IsRtl;

    public NavItem? SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            if (value == null) return;
            if (value.IsLocked) return;         // no device
            if (IsBlockedByMode(value))         // wrong mode
            {
                _selectedNavItem = value;
                OnPropertyChanged();
                CurrentView = value.ViewModel;
                RaiseAccessChanged();
                return;
            }

            _selectedNavItem = value;
            OnPropertyChanged();
            CurrentView = value.ViewModel;
            RaiseAccessChanged();
        }
    }

    public object? CurrentView
    {
        get => _currentView;
        set { _currentView = value; OnPropertyChanged(); }
    }

    public bool IsDeviceConnected => _deviceState.HasDevice;

    public bool IsCurrentPageBlocked
    {
        get
        {
            if (_selectedNavItem == null) return false;
            if (!_deviceState.HasDevice) return false;
            return IsBlockedByMode(_selectedNavItem);
        }
    }

    public string CurrentAccessHint
    {
        get
        {
            if (_selectedNavItem == null) return "";
            if (!_deviceState.HasDevice) return LocalizationService.Translate("Access.NoDeviceAtAll");
            if (!IsBlockedByMode(_selectedNavItem)) return "";

            // Choose message based on what the menu actually needs
            var modes = _selectedNavItem.RequiredModes;
            if (modes.Length == 1 && modes[0] == "adb") return LocalizationService.Translate("Access.OnlyAdbNormal");
            if (modes.Length == 1 && modes[0] == "fastboot") return LocalizationService.Translate("Access.OnlyFastboot");
            if (modes.Length == 1 && modes[0] == "fastbootd") return LocalizationService.Translate("Access.OnlyFastbootd");
            if (modes.Contains("fastboot") && modes.Contains("fastbootd") && modes.Length == 2)
                return LocalizationService.Translate("Access.OnlyFastboot");
            return LocalizationService.Translate("Access.Subtitle");
        }
    }

    public string CurrentModeLabel
    {
        get
        {
            if (!_deviceState.HasDevice) return "Disconnected";
            var d = _deviceState.CurrentDevice;
            if (d == null) return "Disconnected";
            if (d.IsFastboot)
                return d.State?.Contains("fastbootd") == true ? "Fastbootd" : "Fastboot";
            return d.State switch
            {
                "device" => "ADB (Normal)",
                "recovery" => "ADB Recovery",
                "unauthorized" => "Unauthorized",
                _ => d.State
            };
        }
    }

    private bool IsBlockedByMode(NavItem item)
    {
        var current = CurrentModeKey();
        if (string.IsNullOrEmpty(current)) return true;   // no device
        return !item.RequiredModes.Contains(current, StringComparer.OrdinalIgnoreCase);
    }

    private string CurrentModeKey()
    {
        if (!_deviceState.HasDevice) return "";
        var d = _deviceState.CurrentDevice;
        if (d == null) return "";
        if (d.IsFastboot)
        {
            // Distinguish fastbootd from bootloader - some fastbootd devices report as "fastbootd"
            return d.State?.Contains("fastbootd") == true ? "fastbootd" : "fastboot";
        }
        return d.State switch
        {
            "device" => "adb",
            "recovery" => "recovery",
            "unauthorized" => "unauthorized",
            _ => "adb"
        };
    }

    /// <summary>Find the first menu item that is accessible in the current mode.</summary>
    private NavItem? FindSafeMenuItem()
    {
        // Prefer Devices, then Dashboard, then anything accessible
        var preferred = new[] { "Nav.Devices", "Nav.Dashboard", "Nav.Reboot", "Nav.Settings", "Nav.About" };
        foreach (var key in preferred)
        {
            var item = NavItems.FirstOrDefault(n => n.TitleKey == key);
            if (item != null && !item.IsLocked && !IsBlockedByMode(item)) return item;
        }
        return NavItems.FirstOrDefault(n => !n.IsLocked && !IsBlockedByMode(n));
    }

    public void NavigateTo(string titleKey)
    {
        var item = NavItems.FirstOrDefault(n => n.TitleKey == titleKey);
        if (item == null || item.IsLocked) return;
        SelectedNavItem = item;
    }

    public bool ShowConnectedFlash
    {
        get => _showConnectedFlash;
        set { _showConnectedFlash = value; OnPropertyChanged(); }
    }

    public string FlashMessage
    {
        get => _flashMessage;
        set { _flashMessage = value; OnPropertyChanged(); }
    }

    private void ShowFlash(string message)
    {
        FlashMessage = message;
        ShowConnectedFlash = true;

        _flashTimer?.Stop();
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _flashTimer.Tick += (_, _) =>
        {
            _flashTimer?.Stop();
            _flashTimer = null;
            ShowConnectedFlash = false;
        };
        _flashTimer.Start();
    }

    private void UpdateLockStates()
    {
        bool hasDevice = _deviceState.HasDevice;
        foreach (var item in NavItems)
        {
            item.IsLocked = !hasDevice;                              // device disconnected
            item.IsModeBlocked = hasDevice && IsBlockedByMode(item); // device connected but wrong mode
        }
    }

    private void RaiseAccessChanged()
    {
        OnPropertyChanged(nameof(IsCurrentPageBlocked));
        OnPropertyChanged(nameof(CurrentAccessHint));
        OnPropertyChanged(nameof(CurrentModeLabel));
    }

    // ═══════════════════════════════════════════════════════════
    //  NAV ITEM BUILDER - strict access matrix
    // ═══════════════════════════════════════════════════════════
    private IEnumerable<NavItem> BuildNavItems()
    {
        var sp = App.Services;

        // ─── MAIN ───
        // Dashboard: full data ONLY in adb mode
        yield return new NavItem("Nav.Dashboard", "\uE80F", "Group.MAIN",
            sp.GetRequiredService<DashboardViewModel>(), "#4F8CFF",
            AdbOnly, "Access.OnlyAdbNormal");

        // Devices: shows list in any mode
        yield return new NavItem("Nav.Devices", "\uE8EA", "Group.MAIN",
            sp.GetRequiredService<DevicesViewModel>(), "#22D3EE",
            FreeAlways, "Access.Subtitle");

        // ─── DEVICE ───
        yield return new NavItem("Nav.DeviceInfo", "\uE946", "Group.DEVICE",
            sp.GetRequiredService<DeviceInfoViewModel>(), "#60A5FA",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.FileManager", "\uE8B7", "Group.DEVICE",
            sp.GetRequiredService<FileManagerViewModel>(), "#FBBF24",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.AppsManager", "\uE71D", "Group.DEVICE",
            sp.GetRequiredService<AppsManagerViewModel>(), "#34D399",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.Contacts", "\uE8BD", "Group.DEVICE",
            sp.GetRequiredService<ContactsSmsViewModel>(), "#A855F7",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.MediaGallery", "\uE91B", "Group.DEVICE",
            sp.GetRequiredService<MediaGalleryViewModel>(), "#EC4899",
            AdbOnly, "Access.OnlyAdbNormal");

        // ─── APPS ───
        yield return new NavItem("Nav.ApkInstaller", "\uE7B8", "Group.APPS",
            sp.GetRequiredService<ApkInstallerViewModel>(), "#34D399",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.AppBackup", "\uE74E", "Group.APPS",
            sp.GetRequiredService<ApkBackupViewModel>(), "#22D3EE",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.SplitApk", "\uE8A7", "Group.APPS",
            sp.GetRequiredService<SplitApkToolsViewModel>(), "#A855F7",
            AdbOnly, "Access.OnlyAdbNormal");

        // ─── TOOLS ───
        yield return new NavItem("Nav.BackupRestore", "\uE74E", "Group.TOOLS",
            sp.GetRequiredService<BackupRestoreViewModel>(), "#A855F7",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.ScreenMirror", "\uE7F4", "Group.TOOLS",
            sp.GetRequiredService<ScreenMirrorViewModel>(), "#22D3EE",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.ScreenshotRecord", "\uE722", "Group.TOOLS",
            sp.GetRequiredService<ScreenshotRecordViewModel>(), "#EC4899",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.WirelessAdb", "\uE701", "Group.TOOLS",
            sp.GetRequiredService<WirelessAdbViewModel>(), "#F59E0B",
            AdbOnly, "Access.OnlyAdbNormal");

        // ─── FLASH ───
        yield return new NavItem("Nav.FlashTools", "\uE945", "Group.FLASH",
            sp.GetRequiredService<FlashToolsViewModel>(), "#F59E0B",
            FastbootOrFastbootd, "Access.OnlyFastboot");

        yield return new NavItem("Nav.BootloaderTools", "\uE7EF", "Group.FLASH",
            sp.GetRequiredService<BootloaderViewModel>(), "#EF4444",
            FastbootOrFastbootd, "Access.OnlyFastboot");

        // Recovery Manager: just reboot commands - works everywhere
        yield return new NavItem("Nav.RecoveryManager", "\uE777", "Group.FLASH",
            sp.GetRequiredService<RecoveryManagerViewModel>(), "#14B8A6",
            FreeAlways, "Access.Subtitle");

        // Partition Tools: needs fastbootd (userspace)
        yield return new NavItem("Nav.PartitionTools", "\uE9D5", "Group.FLASH",
            sp.GetRequiredService<PartitionToolsViewModel>(), "#818CF8",
            FastbootdOnly, "Access.OnlyFastbootd");

        // ─── ADVANCED ───
        yield return new NavItem("Nav.AdbConsole", "\uE756", "Group.ADVANCED",
            sp.GetRequiredService<AdbConsoleViewModel>(), "#14B8A6",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.FastbootConsole", "\uE945", "Group.ADVANCED",
            sp.GetRequiredService<FastbootConsoleViewModel>(), "#FBBF24",
            FastbootOrFastbootd, "Access.OnlyFastboot");

        yield return new NavItem("Nav.LogcatViewer", "\uE8FD", "Group.ADVANCED",
            sp.GetRequiredService<LogcatViewModel>(), "#EC4899",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.ProcessManager", "\uE9D9", "Group.ADVANCED",
            sp.GetRequiredService<ProcessManagerViewModel>(), "#EF4444",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.SensorsPanel", "\uE9D9", "Group.ADVANCED",
            sp.GetRequiredService<SensorsPanelViewModel>(), "#F59E0B",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.StorageAnalyzer", "\uE8B7", "Group.ADVANCED",
            sp.GetRequiredService<StorageAnalyzerViewModel>(), "#FBBF24",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.BatteryLab", "\uE83F", "Group.ADVANCED",
            sp.GetRequiredService<BatteryLabViewModel>(), "#34D399",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.NetworkTools", "\uE968", "Group.ADVANCED",
            sp.GetRequiredService<NetworkToolsViewModel>(), "#22D3EE",
            AdbOnly, "Access.OnlyAdbNormal");

        // ─── SECURITY ───
        yield return new NavItem("Nav.SecurityCenter", "\uE72E", "Group.SECURITY",
            sp.GetRequiredService<SecurityViewModel>(), "#EF4444",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.PermissionsManager", "\uE8D7", "Group.SECURITY",
            sp.GetRequiredService<PermissionsViewModel>(), "#DC2626",
            AdbOnly, "Access.OnlyAdbNormal");

        yield return new NavItem("Nav.PrivacyTools", "\uE1F6", "Group.SECURITY",
            sp.GetRequiredService<PrivacyViewModel>(), "#A855F7",
            AdbOnly, "Access.OnlyAdbNormal");

        // ─── AUTOMATION ───
        yield return new NavItem("Nav.ScriptRunner", "\uE943", "Group.AUTOMATION",
            sp.GetRequiredService<ScriptRunnerViewModel>(), "#14B8A6",
            FreeAlways, "Access.Subtitle");

        yield return new NavItem("Nav.TaskScheduler", "\uE787", "Group.AUTOMATION",
            sp.GetRequiredService<TaskSchedulerViewModel>(), "#818CF8",
            FreeAlways, "Access.Subtitle");

        // ─── SYSTEM ───
        yield return new NavItem("Nav.Reboot", "\uE777", "Group.SYSTEM",
            sp.GetRequiredService<RebootViewModel>(), "#EF4444",
            FreeAlways, "Access.Subtitle");

        yield return new NavItem("Nav.Settings", "\uE713", "Group.SYSTEM",
            sp.GetRequiredService<SettingsViewModel>(), "#94A3B8",
            FreeAlways, "Access.Subtitle");

        yield return new NavItem("Nav.About", "\uE946", "Group.SYSTEM",
            sp.GetRequiredService<AboutViewModel>(), "#4F8CFF",
            FreeAlways, "Access.Subtitle");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class NavItem : INotifyPropertyChanged
{
    private bool _isLocked;
    private bool _isModeBlocked;

    public NavItem(string titleKey, string glyph, string groupKey, object viewModel,
                    string accentHex, string[] requiredModes, string accessHintKey)
    {
        TitleKey = titleKey;
        Glyph = glyph;
        GroupKey = groupKey;
        ViewModel = viewModel;
        RequiredModes = requiredModes ?? new[] { "adb" };
        AccessHintKey = accessHintKey ?? "Access.Subtitle";

        var color = (Color)ColorConverter.ConvertFromString(accentHex);
        AccentColor = color;

        IconBrush = new SolidColorBrush(color); IconBrush.Freeze();
        IconBoxBrush = new SolidColorBrush(Color.FromArgb(38, color.R, color.G, color.B)); IconBoxBrush.Freeze();
        SelectionBrush = new SolidColorBrush(Color.FromArgb(56, color.R, color.G, color.B)); SelectionBrush.Freeze();
        HoverBrush = new SolidColorBrush(Color.FromArgb(26, color.R, color.G, color.B)); HoverBrush.Freeze();

        LocalizationService.LanguageChanged += (_, _) => RaiseLocalizedChanged();
    }

    public string TitleKey { get; }
    public string GroupKey { get; }
    public string Glyph { get; }
    public object ViewModel { get; }
    public Color AccentColor { get; }
    public SolidColorBrush IconBrush { get; }
    public SolidColorBrush IconBoxBrush { get; }
    public SolidColorBrush SelectionBrush { get; }
    public SolidColorBrush HoverBrush { get; }

    public string[] RequiredModes { get; }
    public string AccessHintKey { get; }

    public string Title => LocalizationService.Translate(TitleKey);
    public string Group => LocalizationService.Translate(GroupKey);

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value) return;
            _isLocked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocked)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLockIcon)));
        }
    }

    /// <summary>True when device is connected but current mode doesn't allow this menu.</summary>
    public bool IsModeBlocked
    {
        get => _isModeBlocked;
        set
        {
            if (_isModeBlocked == value) return;
            _isModeBlocked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModeBlocked)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLockIcon)));
        }
    }

    public bool IsEnabled => !IsLocked;

    /// <summary>True if a lock icon should be visible on this item.</summary>
    public bool ShowLockIcon => IsLocked || IsModeBlocked;

    private void RaiseLocalizedChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
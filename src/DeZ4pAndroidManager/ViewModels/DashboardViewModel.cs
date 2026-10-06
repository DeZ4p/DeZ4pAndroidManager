// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly DeviceInfoService _deviceInfo;
    private readonly ActivityLogService _activity;
    private readonly AnalyticsService _analytics;
    private readonly DeviceStateService _deviceState;
    private readonly FastbootService _fastboot;

    private DeviceInfoModel? _info;
    private FastbootInfo? _fastbootInfo;
    private DeviceModel? _primaryDevice;
    private BatteryDeep? _batteryDeep;
    private string _adbVersion = "detecting...";
    private bool _isAdbRunning;
    private bool _isRefreshing;
    private int _deviceCount;
    private string _lastUpdated = "never";
    private int _healthScore = -1;
    private string _healthGrade = "N/A";
    private string _healthColor = "#94A3B8";

    public DashboardViewModel(AdbService adb, DeviceInfoService deviceInfo,
                              ActivityLogService activity, AnalyticsService analytics,
                              DeviceStateService deviceState, DeviceWatcherService watcher,
                              FastbootService fastboot)
    {
        _adb = adb;
        _deviceInfo = deviceInfo;
        _activity = activity;
        _analytics = analytics;
        _deviceState = deviceState;
        _fastboot = fastboot;

        _activity.StatsChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RaiseStatsChanged);

        // ⭐ React to any device state change (from watcher)
        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try { await RefreshFromStateAsync(); } catch { }
            });
        };

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ClearActivityCommand = new RelayCommand(_ => _activity.Clear());
    }

    public string Version => "v9.1 Pro";
    public string SystemOs => WindowsInfoService.GetFriendlyName();
    public string SystemBuild => $"Build {WindowsInfoService.GetBuildNumber()}";
    public string DotnetVersion => $".NET {Environment.Version}";
    public string MachineName => Environment.MachineName;

    public string AdbVersion { get => _adbVersion; set { _adbVersion = value; OnPropertyChanged(); } }
    public bool IsAdbRunning { get => _isAdbRunning; set { _isAdbRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(AdbStatusLabel)); } }
    public string AdbStatusLabel => IsAdbRunning ? "Running" : "Stopped";

    public DeviceModel? PrimaryDevice
    {
        get => _primaryDevice;
        set
        {
            _primaryDevice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(DeviceTitle));
            OnPropertyChanged(nameof(DeviceSubtitle));
            OnPropertyChanged(nameof(IsFastbootMode));
        }
    }

    public DeviceInfoModel? Info
    {
        get => _info;
        set { _info = value; OnPropertyChanged(); RaiseInfoChanged(); }
    }

    public FastbootInfo? FastbootInfo
    {
        get => _fastbootInfo;
        set { _fastbootInfo = value; OnPropertyChanged(); RaiseFastbootChanged(); }
    }

    public BatteryDeep? BatteryDeepInfo
    {
        get => _batteryDeep;
        set { _batteryDeep = value; OnPropertyChanged(); RaiseBatteryDeepChanged(); }
    }

    public bool HasDevice => PrimaryDevice != null;
    public bool IsFastbootMode => PrimaryDevice?.IsFastboot ?? false;

    public string DeviceTitle => PrimaryDevice?.Model
        ?? (IsFastbootMode ? FastbootInfo?.Product ?? "Fastboot Device" : null)
        ?? PrimaryDevice?.Serial
        ?? "No device connected";

    public string DeviceSubtitle => PrimaryDevice == null
        ? "Plug in a device via USB"
        : $"Serial: {PrimaryDevice.Serial}";

    // ═══ FASTBOOT PROPERTIES ═══
    public string FastbootProductText => FastbootInfo?.Product ?? "N/A";
    public string FastbootBootloaderText => FastbootInfo?.BootloaderVersion ?? "N/A";
    public string FastbootBasebandText => FastbootInfo?.Baseband ?? "N/A";
    public string FastbootUnlockedText => FastbootInfo?.UnlockedDisplay ?? "N/A";
    public string FastbootSecureText => FastbootInfo?.SecureDisplay ?? "N/A";
    public string FastbootSlotText => FastbootInfo?.CurrentSlot ?? "N/A";
    public string FastbootVoltageText => FastbootInfo?.BatteryVoltage ?? "N/A";
    public string FastbootMaxDownloadText => FastbootInfo?.MaxDownloadSize ?? "N/A";

    // ═══ ADB PROPERTIES (N/A in Fastboot) ═══
    public string BatteryDisplay => Info?.BatteryDisplay ?? "N/A";
    public string BatteryHealth => Info?.BatteryHealth ?? "N/A";
    public double BatteryBarValue => Info?.BatteryLevel is int b && b >= 0 ? b : 0;

    public string StorageDisplay => Info == null || Info.StorageUsedPercent < 0 ? "N/A" : $"{Info.StorageUsedPercent}% used";
    public string StorageDetail => Info == null || Info.StorageTotalGb < 0 ? "N/A" : $"{Info.StorageUsedGb:0} / {Info.StorageTotalGb:0} GB";
    public double StorageBarValue => Info?.StorageUsedPercent is int s && s >= 0 ? s : 0;

    public string RamDisplay => Info == null || Info.RamTotalGb < 0 ? "N/A" : $"{Info.RamTotalGb:0.0} GB";
    public string RamDetail => Info == null || Info.RamFreeGb < 0 ? "N/A" : $"{Info.RamFreeGb:0.0} GB free";
    public double RamBarValue
    {
        get
        {
            if (Info == null || Info.RamTotalGb <= 0 || Info.RamUsedGb < 0) return 0;
            return Info.RamUsedGb / Info.RamTotalGb * 100;
        }
    }

    public string AndroidVersion => Info?.AndroidVersion ?? "?";
    public string CpuAbi => Info?.CpuAbi ?? "?";

    public string BatteryLevelText => Info?.BatteryDisplay ?? "N/A";
    public string BatteryTempText => Info == null || Info.BatteryTempC < 0 ? "N/A" : $"{Info.BatteryTempC:0.0} degC";
    public string BatteryVoltageText => Info == null || Info.BatteryVoltageMv < 0 ? "N/A" : $"{Info.BatteryVoltageMv / 1000.0:0.00} V";

    public string StorageTotalText => Info == null || Info.StorageTotalGb < 0 ? "N/A" : $"{Info.StorageTotalGb:0.0} GB";
    public string StorageUsedText => Info == null || Info.StorageUsedGb < 0 ? "N/A" : $"{Info.StorageUsedGb:0.0} GB";
    public string StorageFreeText => Info == null || Info.StorageFreeGb < 0 ? "N/A" : $"{Info.StorageFreeGb:0.0} GB";
    public string StoragePercentText => Info == null || Info.StorageUsedPercent < 0 ? "N/A" : $"{Info.StorageUsedPercent}%";

    public string RamTotalText => Info == null || Info.RamTotalGb < 0 ? "N/A" : $"{Info.RamTotalGb:0.00} GB";
    public string RamUsedText => Info == null || Info.RamUsedGb < 0 ? "N/A" : $"{Info.RamUsedGb:0.00} GB";
    public string RamFreeText => Info == null || Info.RamFreeGb < 0 ? "N/A" : $"{Info.RamFreeGb:0.00} GB";
    public string SwapTotalText => Info == null || Info.SwapTotalGb <= 0 ? "Disabled" : $"{Info.SwapTotalGb:0.00} GB";

    public string KernelShort
    {
        get
        {
            var k = Info?.KernelVersion ?? "?";
            return k.Length > 26 ? k.Substring(0, 26) : k;
        }
    }
    public string UptimeText => Info?.Uptime ?? "N/A";
    public string SecurityPatchText => Info?.SecurityPatch ?? "N/A";

    public string BootloaderText => IsFastbootMode
        ? FastbootInfo?.UnlockedDisplay ?? "N/A"
        : Info?.BootloaderState ?? "N/A";
    public string RootStatusText => Info?.RootStatus ?? "N/A";
    public string SelinuxText => Info?.SelinuxStatus ?? "N/A";
    public string VerifiedBootText => Info?.VerifiedBoot ?? "N/A";

    public string WifiSsidText => Info?.WifiSsid ?? "N/A";
    public string WifiIpText => Info?.WifiIp ?? "N/A";
    public string WifiRssiText => Info == null || Info.WifiRssi == 0 ? "N/A" : $"{Info.WifiRssi} dBm";
    public string ConnectionTypeText => Info?.ConnectionType ?? "N/A";

    public string ScreenResolutionText => Info?.ScreenResolution ?? "N/A";
    public string ScreenDensityText => Info == null || Info.ScreenDensity < 0 ? "N/A" : $"{Info.ScreenDensity} dpi";
    public string ScreenSizeText => Info == null || Info.ScreenInches < 0 ? "N/A" : $"{Info.ScreenInches:0.0}\"";

    public string CpuModelText => Info?.CpuModel ?? "N/A";
    public string CpuCoresText => Info == null || Info.CpuCores < 0 ? "N/A" : $"{Info.CpuCores}";
    public string CpuLoadText => Info == null || Info.CpuLoadPercent < 0 ? "N/A" : $"{Info.CpuLoadPercent:0}%";
    public string CpuAbiText => Info?.CpuAbi ?? "N/A";

    public ObservableCollection<StorageCategory> StorageBreakdown { get; } = new();
    public ObservableCollection<TopApp> TopApps { get; } = new();

    public int HealthScore { get => _healthScore; set { _healthScore = value; OnPropertyChanged(); OnPropertyChanged(nameof(HealthScoreDisplay)); } }
    public string HealthScoreDisplay => HealthScore < 0 ? "-" : HealthScore.ToString();
    public string HealthGrade { get => _healthGrade; set { _healthGrade = value; OnPropertyChanged(); } }
    public string HealthColor { get => _healthColor; set { _healthColor = value; OnPropertyChanged(); } }

    public int DeviceCount { get => _deviceCount; set { _deviceCount = value; OnPropertyChanged(); } }
    public int TodayInstalls => _activity.TodayInstalls;
    public int TotalInstalls => _activity.TotalInstalls;
    public int TodayBackups => _activity.TodayBackups;
    public int TotalBackups => _activity.TotalBackups;

    public string LastUpdated { get => _lastUpdated; set { _lastUpdated = value; OnPropertyChanged(); } }
    public bool IsRefreshing { get => _isRefreshing; set { _isRefreshing = value; OnPropertyChanged(); } }

    public ObservableCollection<ActivityEntry> RecentActivity => _activity.Entries;

    public ICommand RefreshCommand { get; }
    public ICommand ClearActivityCommand { get; }

    public async Task InitializeAsync()
    {
        await RefreshAdbVersionAsync();
        await RefreshFromStateAsync();
    }

    /// <summary>Public refresh - triggered by Refresh button.</summary>
    public async Task RefreshAsync()
    {
        await RefreshAdbVersionAsync();
        await RefreshFromStateAsync();
    }

    private async Task RefreshAdbVersionAsync()
    {
        // Cache: only fetch once
        if (AdbVersion != "detecting..." && AdbVersion != "unknown" && AdbVersion != "adb not found")
            return;

        try
        {
            if (_adb.IsAdbAvailable)
            {
                var v = await _adb.ExecuteRawAsync("version");
                var firstLine = v.StandardOutput.Split('\n').FirstOrDefault() ?? "";
                AdbVersion = firstLine.Replace("Android Debug Bridge version", "v").Trim();
                if (string.IsNullOrEmpty(AdbVersion)) AdbVersion = "unknown";
                IsAdbRunning = true;
            }
            else
            {
                AdbVersion = "adb not found";
                IsAdbRunning = false;
            }
        }
        catch { /* silent */ }
    }

    /// <summary>Fetch data based on current mode (ADB/Fastboot).</summary>
    private async Task RefreshFromStateAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;

        bool analyticsStarted = false;

        try
        {
            var device = _deviceState.CurrentDevice;
            PrimaryDevice = device;
            DeviceCount = device != null ? 1 : 0;

            // ─── No device ───
            if (device == null)
            {
                Info = null;
                FastbootInfo = null;
                BatteryDeepInfo = null;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StorageBreakdown.Clear();
                    TopApps.Clear();
                });
                HealthScore = -1;
                HealthGrade = "N/A";
                HealthColor = "#94A3B8";
                LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                return;
            }

            // ─── Fastboot mode ───
            if (device.IsFastboot)
            {
                Info = null;
                BatteryDeepInfo = null;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StorageBreakdown.Clear();
                    TopApps.Clear();
                });
                FastbootInfo = await _fastboot.GetInfoAsync(device.Serial);
                HealthScore = -1;
                HealthGrade = "N/A";
                HealthColor = "#94A3B8";
                LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                return;
            }

            // ─── ADB mode - FAST PATH ───
            FastbootInfo = null;

            // Show cached analytics immediately (if available)
            var serial = device.Serial;
            if (_analytics.TryGetCachedBattery(serial, out var cachedBatt) && cachedBatt != null)
                BatteryDeepInfo = cachedBatt;
            if (_analytics.TryGetCachedStorage(serial, out var cachedStorage) && cachedStorage != null)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StorageBreakdown.Clear();
                    foreach (var s in cachedStorage) StorageBreakdown.Add(s);
                });
            }
            if (_analytics.TryGetCachedTopApps(serial, out var cachedApps) && cachedApps != null)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    TopApps.Clear();
                    foreach (var a in cachedApps) TopApps.Add(a);
                });
            }

            // Fetch device info (fast)
            Info = await _deviceInfo.GetAsync(serial);
            RecalcHealth();

            LastUpdated = DateTime.Now.ToString("HH:mm:ss");

            // ⭐ Fire analytics in background - UI already shows device info
            analyticsStarted = true;
            _ = Task.Run(async () =>
            {
                try { await RefreshAnalyticsAsync(); }
                catch { }
            });
        }
        catch (Exception ex)
        {
            LastUpdated = "error";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsRefreshing = false;
            RaiseStatsChanged();
            if (!analyticsStarted) { /* nothing */ }
        }
    }

    private async Task RefreshAnalyticsAsync()
    {
        if (PrimaryDevice == null || PrimaryDevice.IsFastboot) return;
        var serial = PrimaryDevice.Serial;

        if (_analytics.TryGetCachedBattery(serial, out var cachedBatt) && cachedBatt != null)
            BatteryDeepInfo = cachedBatt;

        if (_analytics.TryGetCachedStorage(serial, out var cachedStorage) && cachedStorage != null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StorageBreakdown.Clear();
                foreach (var s in cachedStorage) StorageBreakdown.Add(s);
            });
        }

        if (_analytics.TryGetCachedTopApps(serial, out var cachedApps) && cachedApps != null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                TopApps.Clear();
                foreach (var a in cachedApps) TopApps.Add(a);
            });
        }

        RecalcHealth();

        try
        {
            // ⭐ Parallel: battery, storage, top apps at once
            var battTask = _analytics.GetBatteryDeepAsync(serial);
            var storageTask = _analytics.GetStorageBreakdownAsync(serial);
            var appsTask = _analytics.GetTopAppsAsync(serial);

            await Task.WhenAll(battTask, storageTask, appsTask);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                BatteryDeepInfo = battTask.Result;

                StorageBreakdown.Clear();
                foreach (var s in storageTask.Result) StorageBreakdown.Add(s);

                TopApps.Clear();
                foreach (var a in appsTask.Result) TopApps.Add(a);
            });

            RecalcHealth();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private void RecalcHealth()
    {
        if (Info == null) return;
        var (score, grade, color) = AnalyticsService.CalculateHealthScore(
            Info.BatteryLevel, Info.BatteryHealth, Info.StorageUsedPercent,
            Info.RamTotalGb > 0 ? Info.RamUsedGb / Info.RamTotalGb * 100 : 0,
            Info.BatteryTempC, BatteryDeepInfo?.CycleCount ?? 0);
        HealthScore = score;
        HealthGrade = grade;
        HealthColor = color;
    }

    private void RaiseInfoChanged()
    {
        var names = new[]
        {
            nameof(BatteryDisplay), nameof(BatteryHealth), nameof(BatteryBarValue),
            nameof(StorageDisplay), nameof(StorageDetail), nameof(StorageBarValue),
            nameof(RamDisplay), nameof(RamDetail), nameof(RamBarValue),
            nameof(AndroidVersion), nameof(CpuAbi),
            nameof(BatteryLevelText), nameof(BatteryTempText), nameof(BatteryVoltageText),
            nameof(StorageTotalText), nameof(StorageUsedText), nameof(StorageFreeText), nameof(StoragePercentText),
            nameof(RamTotalText), nameof(RamUsedText), nameof(RamFreeText), nameof(SwapTotalText),
            nameof(KernelShort), nameof(UptimeText), nameof(SecurityPatchText),
            nameof(BootloaderText), nameof(RootStatusText), nameof(SelinuxText), nameof(VerifiedBootText),
            nameof(WifiSsidText), nameof(WifiIpText), nameof(WifiRssiText), nameof(ConnectionTypeText),
            nameof(ScreenResolutionText), nameof(ScreenDensityText), nameof(ScreenSizeText),
            nameof(CpuModelText), nameof(CpuCoresText), nameof(CpuLoadText), nameof(CpuAbiText),
        };
        foreach (var n in names) OnPropertyChanged(n);
    }

    private void RaiseFastbootChanged()
    {
        var names = new[]
        {
            nameof(FastbootProductText), nameof(FastbootBootloaderText), nameof(FastbootBasebandText),
            nameof(FastbootUnlockedText), nameof(FastbootSecureText), nameof(FastbootSlotText),
            nameof(FastbootVoltageText), nameof(FastbootMaxDownloadText), nameof(DeviceTitle),
        };
        foreach (var n in names) OnPropertyChanged(n);
    }

    private void RaiseBatteryDeepChanged()
    {
        OnPropertyChanged(nameof(HasBatteryDeep));
        OnPropertyChanged(nameof(CycleCountText));
        OnPropertyChanged(nameof(DesignCapacityText));
        OnPropertyChanged(nameof(CurrentCapacityText));
        OnPropertyChanged(nameof(BatteryHealthPercentText));
        OnPropertyChanged(nameof(BatteryAgeText));
    }

    public bool HasBatteryDeep => BatteryDeepInfo != null &&
        (BatteryDeepInfo.CycleCount > 0 || BatteryDeepInfo.DesignCapacityMah > 0);
    public string CycleCountText => BatteryDeepInfo?.CycleDisplay ?? "N/A";
    public string DesignCapacityText => BatteryDeepInfo?.DesignDisplay ?? "N/A";
    public string CurrentCapacityText => BatteryDeepInfo?.CurrentDisplay ?? "N/A";
    public string BatteryHealthPercentText => BatteryDeepInfo?.HealthPercent ?? "N/A";
    public string BatteryAgeText => BatteryDeepInfo?.AgeDisplay ?? "N/A";

    private void RaiseStatsChanged()
    {
        OnPropertyChanged(nameof(TodayInstalls));
        OnPropertyChanged(nameof(TotalInstalls));
        OnPropertyChanged(nameof(TodayBackups));
        OnPropertyChanged(nameof(TotalBackups));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
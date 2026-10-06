// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class DeviceInfoViewModel : INotifyPropertyChanged
{
    private const double MinHz = 24;
    private const double MaxHz = 240;

    private readonly AdbService _adb;
    private readonly DeviceInfoService _deviceInfo;
    private readonly DeviceStateService _deviceState;
    private readonly FastbootService _fastboot;

    private DeviceInfoModel? _info;
    private FastbootInfo? _fastbootInfo;
    private DeviceModel? _device;
    private bool _isRefreshing;
    private string _lastUpdated = "never";

    private string _brand = "N/A";
    private string _manufacturer = "N/A";
    private string _deviceCodename = "N/A";
    private string _buildId = "N/A";
    private string _buildDisplayId = "N/A";
    private string _sdkLevel = "N/A";
    private string _baseband = "N/A";
    private string _hardware = "N/A";
    private string _gpuRenderer = "N/A";
    private string _wifiMac = "N/A";
    private string _bluetoothName = "N/A";
    private string _simOperator = "N/A";
    private string _networkType = "N/A";
    private string _refreshRate = "N/A";

    public DeviceInfoViewModel(AdbService adb, DeviceInfoService deviceInfo,
                                DeviceStateService deviceState, DeviceWatcherService watcher,
                                FastbootService fastboot)
    {
        _adb = adb;
        _deviceInfo = deviceInfo;
        _deviceState = deviceState;
        _fastboot = fastboot;

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try { await RefreshAsync(); } catch { }
            });
        };

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    // ═══ STATE ═══
    public DeviceModel? CurrentDevice
    {
        get => _device;
        set
        {
            _device = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(IsFastbootMode));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
        }
    }

    public DeviceInfoModel? Info
    {
        get => _info;
        set { _info = value; OnPropertyChanged(); RaiseAdbChanged(); }
    }

    public FastbootInfo? FastbootInfo
    {
        get => _fastbootInfo;
        set { _fastbootInfo = value; OnPropertyChanged(); RaiseFastbootChanged(); }
    }

    public bool HasDevice => CurrentDevice != null;
    public bool IsFastbootMode => CurrentDevice?.IsFastboot ?? false;

    public string HeaderTitle => IsFastbootMode
        ? (FastbootInfo?.Product ?? CurrentDevice?.Serial ?? "Fastboot Device")
        : Info?.Model ?? "No device connected";

    public string HeaderSubtitle => CurrentDevice == null
        ? "Connect a device to see its details"
        : IsFastbootMode
            ? $"Serial: {CurrentDevice.Serial}  -  Fastboot mode"
            : $"Serial: {CurrentDevice.Serial}";

    public string LastUpdated { get => _lastUpdated; set { _lastUpdated = value; OnPropertyChanged(); } }
    public bool IsRefreshing { get => _isRefreshing; set { _isRefreshing = value; OnPropertyChanged(); } }

    // ═══ FASTBOOT-ONLY PROPERTIES ═══
    public string FbProductText => FastbootInfo?.Product ?? "N/A";
    public string FbSerialText => FastbootInfo?.Serial ?? CurrentDevice?.Serial ?? "N/A";
    public string FbBootloaderText => FastbootInfo?.BootloaderVersion ?? "N/A";
    public string FbBasebandText => FastbootInfo?.Baseband ?? "N/A";
    public string FbUnlockedText => FastbootInfo?.UnlockedDisplay ?? "N/A";
    public string FbSecureText => FastbootInfo?.SecureDisplay ?? "N/A";
    public string FbSlotText => FastbootInfo?.CurrentSlot ?? "N/A";
    public string FbVoltageText => FastbootInfo?.BatteryVoltage ?? "N/A";
    public string FbMaxDownloadText => FastbootInfo?.MaxDownloadSize ?? "N/A";
    public string FbPartitionTypeText => FastbootInfo?.PartitionType ?? "N/A";

    // ═══ ADB PROPERTIES ═══
    public string BrandText { get => _brand; set { _brand = value; OnPropertyChanged(); } }
    public string ManufacturerText { get => _manufacturer; set { _manufacturer = value; OnPropertyChanged(); } }
    public string ModelText => Info?.Model ?? "N/A";
    public string DeviceCodenameText { get => _deviceCodename; set { _deviceCodename = value; OnPropertyChanged(); } }
    public string AndroidText => Info?.AndroidVersion ?? "N/A";
    public string SdkLevelText { get => _sdkLevel; set { _sdkLevel = value; OnPropertyChanged(); } }
    public string BuildIdText { get => _buildId; set { _buildId = value; OnPropertyChanged(); } }
    public string BuildDisplayIdText { get => _buildDisplayId; set { _buildDisplayId = value; OnPropertyChanged(); } }
    public string SecurityPatchText => Info?.SecurityPatch ?? "N/A";
    public string BuildDateText => Info?.BuildDate ?? "N/A";

    public string CpuModelText => Info?.CpuModel ?? "N/A";
    public string CpuCoresText => Info == null || Info.CpuCores < 0 ? "N/A" : Info.CpuCores.ToString();
    public string CpuAbiText => Info?.CpuAbi ?? "N/A";
    public string GpuRendererText { get => _gpuRenderer; set { _gpuRenderer = value; OnPropertyChanged(); } }
    public string HardwareText { get => _hardware; set { _hardware = value; OnPropertyChanged(); } }
    public string RamTotalText => Info == null || Info.RamTotalGb < 0 ? "N/A" : $"{Info.RamTotalGb:0.00} GB";
    public string RamUsedText => Info == null || Info.RamUsedGb < 0 ? "N/A" : $"{Info.RamUsedGb:0.00} GB";
    public string RamFreeText => Info == null || Info.RamFreeGb < 0 ? "N/A" : $"{Info.RamFreeGb:0.00} GB";
    public string SwapText => Info == null || Info.SwapTotalGb <= 0 ? "Disabled" : $"{Info.SwapTotalGb:0.00} GB";
    public string StorageTotalText => Info == null || Info.StorageTotalGb < 0 ? "N/A" : $"{Info.StorageTotalGb:0.0} GB";

    public string ResolutionText => Info?.ScreenResolution ?? "N/A";
    public string DensityText => Info == null || Info.ScreenDensity < 0 ? "N/A" : $"{Info.ScreenDensity} dpi";
    public string ScreenSizeText => Info == null || Info.ScreenInches < 0 ? "N/A" : $"{Info.ScreenInches:0.0}\"";
    public string RefreshRateText { get => _refreshRate; set { _refreshRate = value; OnPropertyChanged(); } }

    public string BatteryLevelText => Info?.BatteryDisplay ?? "N/A";
    public string BatteryHealthText => Info?.BatteryHealth ?? "N/A";
    public string BatteryTempText => Info == null || Info.BatteryTempC < 0 ? "N/A" : $"{Info.BatteryTempC:0.0} degC";
    public string BatteryVoltageText => Info == null || Info.BatteryVoltageMv < 0 ? "N/A" : $"{Info.BatteryVoltageMv / 1000.0:0.00} V";
    public string BatteryStatusText => Info?.BatteryStatus ?? "N/A";

    public string WifiSsidText => Info?.WifiSsid ?? "N/A";
    public string WifiIpText => Info?.WifiIp ?? "N/A";
    public string WifiMacText { get => _wifiMac; set { _wifiMac = value; OnPropertyChanged(); } }
    public string BluetoothNameText { get => _bluetoothName; set { _bluetoothName = value; OnPropertyChanged(); } }
    public string SimOperatorText { get => _simOperator; set { _simOperator = value; OnPropertyChanged(); } }
    public string NetworkTypeText { get => _networkType; set { _networkType = value; OnPropertyChanged(); } }
    public string ConnectionText => Info?.ConnectionType ?? "N/A";

    public string BootloaderText => Info?.BootloaderState ?? "N/A";
    public string RootStatusText => Info?.RootStatus ?? "N/A";
    public string SelinuxText => Info?.SelinuxStatus ?? "N/A";
    public string VerifiedBootText => Info?.VerifiedBoot ?? "N/A";

    public string KernelText => Info?.KernelVersion ?? "N/A";
    public string BasebandText { get => _baseband; set { _baseband = value; OnPropertyChanged(); } }
    public string UptimeText => Info?.Uptime ?? "N/A";

    public ICommand RefreshCommand { get; }

    public async Task InitializeAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            if (!_adb.IsAdbAvailable && !_fastboot.IsFastbootAvailable)
            {
                CurrentDevice = null;
                Info = null;
                FastbootInfo = null;
                LastUpdated = "tools not found";
                return;
            }

            // Use DeviceStateService - Watcher keeps it up to date
            var device = _deviceState.CurrentDevice;
            CurrentDevice = device;

            if (device == null)
            {
                Info = null;
                FastbootInfo = null;
                ResetExtended();
                LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                return;
            }

            if (device.IsFastboot)
            {
                // ═══ FASTBOOT MODE ═══
                Info = null;
                FastbootInfo = await _fastboot.GetInfoAsync(device.Serial);
                ResetAdbExtended();
            }
            else
            {
                // ═══ ADB MODE ═══
                FastbootInfo = null;
                Info = await _deviceInfo.GetAsync(device.Serial);
                await FetchAdbExtendedAsync(device.Serial);
            }

            LastUpdated = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            LastUpdated = "error";
        }
        finally { IsRefreshing = false; }
    }

    private async Task FetchAdbExtendedAsync(string serial)
    {
        const string cmd =
            "echo BRAND:$(getprop ro.product.brand); " +
            "echo MANUFACTURER:$(getprop ro.product.manufacturer); " +
            "echo DEVICE:$(getprop ro.product.device); " +
            "echo BUILDID:$(getprop ro.build.id); " +
            "echo DISPLAYID:$(getprop ro.build.display.id); " +
            "echo SDK:$(getprop ro.build.version.sdk); " +
            "echo BASEBAND:$(getprop gsm.version.baseband); " +
            "echo HARDWARE:$(getprop ro.hardware); " +
            "echo EGL:$(getprop ro.hardware.egl); " +
            "echo VULKAN:$(getprop ro.hardware.vulkan); " +
            "echo MAC:$(cat /sys/class/net/wlan0/address 2>/dev/null); " +
            "echo BT:$(settings get secure bluetooth_name 2>/dev/null); " +
            "echo SIM:$(getprop gsm.sim.operator.alpha); " +
            "echo NETTYPE:$(getprop gsm.network.type); " +
            "echo REFRESH1:$(getprop debug.hwui.refresh_rate); " +
            "echo REFRESH2:$(getprop ro.surface_flinger.primary_display_refresh_rate); " +
            "echo REFRESH3:$(dumpsys SurfaceFlinger --latency 2>/dev/null | head -1); " +
            "echo REFRESH4:$(dumpsys display 2>/dev/null | grep -m1 -oE 'fps=[0-9.]+' | cut -d= -f2); " +
            "echo REFRESH5:$(dumpsys display 2>/dev/null | grep -m1 -oE '[0-9.]+ ?fps' | grep -oE '[0-9.]+')";

        var result = await _adb.ExecuteRawAsync($"-s {serial} shell \"{cmd}\"", 15000);
        var refreshCandidates = new List<double>();

        foreach (var line in result.StandardOutput.Replace("\r", "").Split('\n'))
        {
            int idx = line.IndexOf(':');
            if (idx <= 0) continue;
            string key = line.Substring(0, idx).Trim();
            string val = line.Substring(idx + 1).Trim();
            if (string.IsNullOrWhiteSpace(val) || val == "null") val = "N/A";

            switch (key)
            {
                case "BRAND": BrandText = val; break;
                case "MANUFACTURER": ManufacturerText = val; break;
                case "DEVICE": DeviceCodenameText = val; break;
                case "BUILDID": BuildIdText = val; break;
                case "DISPLAYID": BuildDisplayIdText = val; break;
                case "SDK": SdkLevelText = val; break;
                case "BASEBAND": BasebandText = val; break;
                case "HARDWARE": HardwareText = val; break;
                case "EGL": if (GpuRendererText == "N/A") GpuRendererText = val; break;
                case "VULKAN": if (GpuRendererText == "N/A") GpuRendererText = val; break;
                case "MAC": WifiMacText = FormatMac(val); break;
                case "BT": BluetoothNameText = val; break;
                case "SIM": SimOperatorText = val; break;
                case "NETTYPE": NetworkTypeText = val; break;
                case "REFRESH1":
                case "REFRESH2":
                case "REFRESH4":
                case "REFRESH5":
                    TryCollectHz(val, refreshCandidates); break;
                case "REFRESH3":
                    TryCollectHzFromNanoseconds(val, refreshCandidates); break;
            }
        }

        if (refreshCandidates.Count > 0)
        {
            var winner = refreshCandidates
                .Select(x => Math.Round(x / 5.0) * 5)
                .GroupBy(x => x)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .First().Key;
            RefreshRateText = $"{winner:0} Hz";
        }
        else RefreshRateText = "N/A";
    }

    private static void TryCollectHz(string raw, List<double> list)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (raw == "N/A" || raw == "0" || raw == "0.0") return;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double hz)) return;
        if (hz < MinHz || hz > MaxHz) return;
        list.Add(hz);
    }

    private static void TryCollectHzFromNanoseconds(string raw, List<double> list)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "N/A") return;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double ns)) return;
        if (ns < 4_000_000 || ns > 50_000_000) return;
        double hz = 1_000_000_000.0 / ns;
        if (hz < MinHz || hz > MaxHz) return;
        list.Add(hz);
    }

    private static string FormatMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return "N/A";
        mac = mac.Trim();
        if (mac == "02:00:00:00:00:00" || mac.Contains("00:00:00:00:00:00"))
            return "Restricted";
        return mac;
    }

    private void ResetAdbExtended()
    {
        BrandText = ManufacturerText = DeviceCodenameText = "N/A";
        BuildIdText = BuildDisplayIdText = SdkLevelText = "N/A";
        BasebandText = HardwareText = GpuRendererText = "N/A";
        WifiMacText = BluetoothNameText = "N/A";
        SimOperatorText = NetworkTypeText = "N/A";
        RefreshRateText = "N/A";
    }

    private void ResetExtended() => ResetAdbExtended();

    private void RaiseAdbChanged()
    {
        var props = new[]
        {
            nameof(HeaderTitle), nameof(HeaderSubtitle),
            nameof(ModelText), nameof(AndroidText), nameof(SecurityPatchText), nameof(BuildDateText),
            nameof(CpuModelText), nameof(CpuCoresText), nameof(CpuAbiText),
            nameof(RamTotalText), nameof(RamUsedText), nameof(RamFreeText), nameof(SwapText),
            nameof(StorageTotalText),
            nameof(ResolutionText), nameof(DensityText), nameof(ScreenSizeText),
            nameof(BatteryLevelText), nameof(BatteryHealthText), nameof(BatteryTempText),
            nameof(BatteryVoltageText), nameof(BatteryStatusText),
            nameof(WifiSsidText), nameof(WifiIpText), nameof(ConnectionText),
            nameof(BootloaderText), nameof(RootStatusText), nameof(SelinuxText), nameof(VerifiedBootText),
            nameof(KernelText), nameof(UptimeText),
        };
        foreach (var p in props) OnPropertyChanged(p);
    }

    private void RaiseFastbootChanged()
    {
        var props = new[]
        {
            nameof(HeaderTitle), nameof(HeaderSubtitle),
            nameof(FbProductText), nameof(FbSerialText), nameof(FbBootloaderText),
            nameof(FbBasebandText), nameof(FbUnlockedText), nameof(FbSecureText),
            nameof(FbSlotText), nameof(FbVoltageText), nameof(FbMaxDownloadText),
            nameof(FbPartitionTypeText),
        };
        foreach (var p in props) OnPropertyChanged(p);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Rich snapshot of a connected Android device - everything optional,
/// defaults to N/A / -1, never throws.
/// </summary>
public class DeviceInfoModel
{
    public string Serial { get; set; } = string.Empty;
    public string Model { get; set; } = "Unknown";
    public string AndroidVersion { get; set; } = "?";
    public string CpuAbi { get; set; } = "?";

    // ─── Battery ───
    public int BatteryLevel { get; set; } = -1;
    public string BatteryHealth { get; set; } = "N/A";
    public double BatteryTempC { get; set; } = -1;
    public int BatteryVoltageMv { get; set; } = -1;
    public string BatteryStatus { get; set; } = "N/A";

    // ─── Storage ───
    public double StorageTotalGb { get; set; } = -1;
    public double StorageUsedGb { get; set; } = -1;
    public double StorageFreeGb { get; set; } = -1;
    public int StorageUsedPercent { get; set; } = -1;

    // ─── SD Card ───
    public bool SdCardDetected { get; set; }
    public string SdCardPath { get; set; } = string.Empty;
    public double SdCardTotalGb { get; set; } = -1;
    public double SdCardUsedGb { get; set; } = -1;
    public double SdCardFreeGb { get; set; } = -1;
    public int SdCardUsedPercent { get; set; } = -1;

    // ─── Memory ───
    public double RamTotalGb { get; set; } = -1;
    public double RamUsedGb { get; set; } = -1;
    public double RamFreeGb { get; set; } = -1;
    public double SwapTotalGb { get; set; } = -1;
    public double SwapUsedGb { get; set; } = -1;
    public double SwapFreeGb { get; set; } = -1;

    // ─── System ───
    public string KernelVersion { get; set; } = "?";
    public string BuildDate { get; set; } = "?";
    public string SecurityPatch { get; set; } = "?";
    public string Uptime { get; set; } = "?";

    // ─── Security (NEW) ───
    public string BootloaderState { get; set; } = "N/A";   // Locked / Unlocked
    public string RootStatus { get; set; } = "N/A";        // Rooted / Not rooted
    public string SelinuxStatus { get; set; } = "N/A";     // Enforcing / Permissive
    public string VerifiedBoot { get; set; } = "N/A";      // green / orange / yellow / red

    // ─── Network (NEW) ───
    public string WifiSsid { get; set; } = "N/A";
    public string WifiIp { get; set; } = "N/A";
    public int WifiRssi { get; set; } = 0;
    public string ConnectionType { get; set; } = "N/A";    // USB / WiFi / Both

    // ─── Display (NEW) ───
    public string ScreenResolution { get; set; } = "N/A";
    public int ScreenDensity { get; set; } = -1;
    public double ScreenInches { get; set; } = -1;
    public double ScreenRefreshHz { get; set; } = -1;

    // ─── CPU (NEW) ───
    public string CpuModel { get; set; } = "N/A";
    public int CpuCores { get; set; } = -1;
    public double CpuLoadPercent { get; set; } = -1;
    public string CpuGovernor { get; set; } = "N/A";

    // ─── UI helpers ───
    public string BatteryDisplay => BatteryLevel < 0 ? "N/A" : $"{BatteryLevel}%";
    public string StorageDisplay => StorageUsedPercent < 0 ? "N/A" : $"{StorageUsedPercent}% used";
    public string RamDisplay => RamTotalGb < 0 ? "N/A" : $"{RamTotalGb:0.0} GB";
}
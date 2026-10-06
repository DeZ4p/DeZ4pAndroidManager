// © DeZ4p | t.me/DeZ4p | All Rights Reserved

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Info collected from `fastboot getvar` when device is in bootloader/fastbootd mode.
/// Only 8-10 fields are ever available in Fastboot - Android System is not running.
/// </summary>
public class FastbootInfo
{
    public string Serial { get; set; } = "N/A";
    public string Product { get; set; } = "N/A";
    public string BootloaderVersion { get; set; } = "N/A";
    public string Baseband { get; set; } = "N/A";
    public string Unlocked { get; set; } = "N/A";
    public string Secure { get; set; } = "N/A";
    public string CurrentSlot { get; set; } = "N/A";
    public string BatteryVoltage { get; set; } = "N/A";
    public string MaxDownloadSize { get; set; } = "N/A";
    public string PartitionType { get; set; } = "N/A";

    public bool HasData => Product != "N/A" || Serial != "N/A";

    public string UnlockedDisplay => Unlocked.ToLowerInvariant() switch
    {
        "yes" => "Unlocked",
        "no" => "Locked",
        _ => Unlocked
    };

    public string SecureDisplay => Secure.ToLowerInvariant() switch
    {
        "yes" => "Secure Boot ON",
        "no" => "Secure Boot OFF",
        _ => Secure
    };
}
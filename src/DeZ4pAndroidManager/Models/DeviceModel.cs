// © DeZ4p | t.me/DeZ4p | All Rights Reserved

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// Represents a connected Android device - either in ADB mode (normal/recovery)
/// or Fastboot mode (bootloader/fastbootd).
/// </summary>
public class DeviceModel
{
    public string Serial { get; set; } = string.Empty;
    public string State { get; set; } = "unknown";

    /// <summary>"adb" or "fastboot" - which protocol the device is visible on.</summary>
    public string Mode { get; set; } = "adb";

    public string? Model { get; set; }
    public string? Product { get; set; }
    public string? Device { get; set; }
    public string? TransportId { get; set; }

    public bool IsFastboot => Mode == "fastboot";

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Model)
            ? $"{Model} ({Serial})"
            : Serial;

    /// <summary>
    /// Device is usable - either ADB "device" state or any fastboot mode.
    /// </summary>
    public bool IsReady => IsFastboot || State == "device";

    public string StateLabel => IsFastboot
        ? "Fastboot"
        : State switch
        {
            "device" => "Ready",
            "unauthorized" => "Unauthorized",
            "offline" => "Offline",
            "bootloader" => "Bootloader",
            "recovery" => "Recovery",
            _ => State
        };

    public override string ToString() => DisplayName;
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;

namespace DeZ4pAndroidManager.Models;

public class BootTarget
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "\uE777";
    public string Color { get; set; } = "#4F8CFF";
    public string Command { get; set; } = "";
    public string RequiredMode { get; set; } = "";  // "adb" | "fastboot" | "" (any)
    public bool IsDestructive { get; set; }
    public string[] SupportedModes { get; set; } = Array.Empty<string>();
}

public class BootHistoryEntry
{
    public string Target { get; set; } = "";
    public string Mode { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    public string TimeDisplay => Time.ToString("HH:mm:ss");
    public string ResultDisplay => Success ? "OK" : "FAIL";
    public string ResultColor => Success ? "#34D399" : "#EF4444";
}
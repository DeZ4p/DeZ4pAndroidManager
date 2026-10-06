// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Text.Json.Serialization;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// One entry in the activity feed shown on the Dashboard.
/// </summary>
public class ActivityEntry
{
    /// <summary>Category: install, backup, reboot, screenshot, info.</summary>
    public string Kind { get; set; } = "info";

    public string Icon { get; set; } = "•";
    public string Message { get; set; } = string.Empty;
    public string AccentHex { get; set; } = "#4F8CFF";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string RelativeTime
    {
        get
        {
            var delta = DateTime.Now - Timestamp;
            if (delta.TotalSeconds < 60) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
            return $"{(int)delta.TotalDays}d ago";
        }
    }
}
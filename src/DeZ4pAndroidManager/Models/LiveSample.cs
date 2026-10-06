// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Collections.ObjectModel;

namespace DeZ4pAndroidManager.Models;

public class ThermalZone
{
    public string Name { get; set; } = "";
    public double TempC { get; set; } = -1;
    public string Display => TempC < 0 ? "N/A" : $"{TempC:0.0}degC";

    public string Icon => Name.ToLowerInvariant() switch
    {
        var n when n.Contains("cpu") => "🔥",
        var n when n.Contains("gpu") => "🎮",
        var n when n.Contains("batt") => "🔋",
        var n when n.Contains("charg") => "⚡",
        var n when n.Contains("skin") => "🌡",
        var n when n.Contains("pa") => "📢",
        _ => "🌡"
    };
}

/// <summary>
/// Top app with combined CPU + RAM score (like popular resource-monitor apps).
/// Score = CPU% * 1.0 + RAM_MB * 0.04
/// </summary>
public class TopApp
{
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public double MemMb { get; set; }
    public double Score { get; set; }

    public string CpuDisplay => CpuPercent <= 0 ? "-" : $"{CpuPercent:0.0}%";
    public string MemDisplay => MemMb <= 0 ? "-" : $"{MemMb:0} MB";
    public string ScoreDisplay => $"{Score:0.0}";
}

public class StorageCategory
{
    public string Icon { get; set; } = "📁";
    public string Name { get; set; } = "";
    public double SizeGb { get; set; }
    public double PercentOfTotal { get; set; }
    public string SizeDisplay => SizeGb < 0 ? "N/A" : $"{SizeGb:0.00} GB";
}

public class BatteryDeep
{
    public int CycleCount { get; set; } = -1;
    public double DesignCapacityMah { get; set; } = -1;
    public double CurrentCapacityMah { get; set; } = -1;
    public string HealthPercent { get; set; } = "N/A";
    public string AgeText { get; set; } = "N/A";
    public string CycleDisplay => CycleCount < 0 ? "N/A" : CycleCount.ToString();
    public string DesignDisplay => DesignCapacityMah < 0 ? "N/A" : $"{DesignCapacityMah:0} mAh";
    public string CurrentDisplay => CurrentCapacityMah < 0 ? "N/A" : $"{CurrentCapacityMah:0} mAh";
    public string AgeDisplay => string.IsNullOrEmpty(AgeText) ? "N/A" : AgeText;
}
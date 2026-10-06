// © DeZ4p | t.me/DeZ4p | All Rights Reserved

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// One real-time sample of device resource usage.
/// </summary>
public class MonitorSample
{
    public double CpuPercent { get; set; } = 0;
    public double RamPercent { get; set; } = 0;
    public double RamUsedGb { get; set; } = 0;
    public double RamTotalGb { get; set; } = 0;
    public double NetUpKbps { get; set; } = 0;
    public double NetDownKbps { get; set; } = 0;
    public double BatteryTempC { get; set; } = -1;
    public double CpuTempC { get; set; } = -1;
    public double LoadAvg1 { get; set; } = -1;

    public string CpuDisplay => CpuPercent < 0 ? "N/A" : $"{CpuPercent:0}%";
    public string RamDisplay => RamTotalGb <= 0 ? "N/A" : $"{RamPercent:0}%";
    public string RamDetail => RamTotalGb <= 0 ? "N/A" : $"{RamUsedGb:0.00} / {RamTotalGb:0.00} GB";
    public string NetUpDisplay => FormatSpeed(NetUpKbps);
    public string NetDownDisplay => FormatSpeed(NetDownKbps);
    public string LoadDisplay => LoadAvg1 < 0 ? "N/A" : $"{LoadAvg1:0.00}";

    private static string FormatSpeed(double kbps)
    {
        if (kbps < 0) return "N/A";
        if (kbps < 1) return "0 KB/s";
        if (kbps < 1024) return $"{kbps:0} KB/s";
        return $"{kbps / 1024.0:0.00} MB/s";
    }
}
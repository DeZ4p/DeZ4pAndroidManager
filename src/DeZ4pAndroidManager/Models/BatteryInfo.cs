// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

public class BatteryInfo
{
    public int Level { get; set; } = -1;
    public string Health { get; set; } = "N/A";
    public string Status { get; set; } = "N/A";
    public double TemperatureC { get; set; } = -1;
    public int VoltageMv { get; set; } = -1;
    public int CurrentMa { get; set; } = -1;
    public string Technology { get; set; } = "N/A";
    public int DesignCapacityMah { get; set; } = -1;
    public int CurrentCapacityMah { get; set; } = -1;
    public int CycleCount { get; set; } = -1;
    public string ChargeSource { get; set; } = "N/A";
    public int MaxChargingCurrentMa { get; set; } = -1;
    public int MaxChargingVoltageMv { get; set; } = -1;
    public string PowerProfile { get; set; } = "N/A";

    public string LevelDisplay => Level < 0 ? "N/A" : $"{Level}%";
    public string TempDisplay => TemperatureC < 0 ? "N/A" : $"{TemperatureC:0.0} Â°C";
    public string VoltageDisplay => VoltageMv < 0 ? "N/A" : $"{VoltageMv / 1000.0:0.00} V";
    public string CurrentDisplay => CurrentMa == 0 ? "N/A" : $"{CurrentMa} mA";

    public string HealthPercent
    {
        get
        {
            if (DesignCapacityMah <= 0 || CurrentCapacityMah <= 0) return "N/A";
            var p = CurrentCapacityMah * 100.0 / DesignCapacityMah;
            if (p < 0 || p > 200) return "N/A";
            return $"{p:0}%";
        }
    }

    public string CapacityDisplay => DesignCapacityMah <= 0 && CurrentCapacityMah <= 0
        ? "N/A"
        : $"{(CurrentCapacityMah > 0 ? CurrentCapacityMah : 0)} / {(DesignCapacityMah > 0 ? DesignCapacityMah : 0)} mAh";

    public string CycleDisplay => CycleCount <= 0 ? "N/A" : CycleCount.ToString();

    public Brush LevelColor
    {
        get
        {
            var hex = Level switch
            {
                >= 80 => "#34D399",
                >= 50 => "#4F8CFF",
                >= 20 => "#F59E0B",
                >= 0  => "#EF4444",
                _     => "#6B7280"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze(); return b;
        }
    }
}
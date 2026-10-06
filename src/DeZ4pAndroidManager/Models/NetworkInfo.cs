// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Collections.ObjectModel;

namespace DeZ4pAndroidManager.Models;

public class NetworkInterfaceInfo
{
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public string Ip { get; set; } = "â€”";
    public string Mac { get; set; } = "â€”";
    public string Mtu { get; set; } = "â€”";
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }

    public string RxDisplay => MediaItem.FormatSize(RxBytes);
    public string TxDisplay => MediaItem.FormatSize(TxBytes);
    public bool IsUp => State.Equals("UP", System.StringComparison.OrdinalIgnoreCase);
}

public class NetworkInfo
{
    // WiFi
    public string WifiSsid { get; set; } = "N/A";
    public string WifiIp { get; set; } = "N/A";
    public string WifiMac { get; set; } = "N/A";
    public int WifiRssi { get; set; } = 0;
    public int WifiLinkSpeedMbps { get; set; } = 0;
    public int WifiFrequencyMhz { get; set; } = 0;

    // Mobile
    public string MobileOperator { get; set; } = "N/A";
    public string MobileNetworkType { get; set; } = "N/A";
    public string MobileState { get; set; } = "N/A";

    // DNS / Route
    public string Dns1 { get; set; } = "N/A";
    public string Dns2 { get; set; } = "N/A";
    public string DefaultGateway { get; set; } = "N/A";

    // Wi-Fi IP (auxiliary)
    public ObservableCollection<NetworkInterfaceInfo> Interfaces { get; } = new();

    public string RssiDisplay => WifiRssi == 0 ? "N/A" : $"{WifiRssi} dBm";
    public string LinkSpeedDisplay => WifiLinkSpeedMbps <= 0 ? "N/A" : $"{WifiLinkSpeedMbps} Mbps";
    public string FrequencyDisplay => WifiFrequencyMhz <= 0 ? "N/A" : $"{WifiFrequencyMhz} MHz";

    public string RssiQuality
    {
        get
        {
            if (WifiRssi == 0) return "N/A";
            if (WifiRssi >= -50) return "Excellent";
            if (WifiRssi >= -60) return "Good";
            if (WifiRssi >= -70) return "Fair";
            if (WifiRssi >= -80) return "Weak";
            return "Very Weak";
        }
    }
}
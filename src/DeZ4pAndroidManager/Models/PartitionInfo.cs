// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System.Windows.Media;
namespace DeZ4pAndroidManager.Models;

public class PartitionInfo
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Type { get; set; } = "";

    public Brush TypeBrush
    {
        get
        {
            var hex = Type.Contains("ext4") ? "#4F8CFF"
                    : Type.Contains("f2fs") ? "#22D3EE"
                    : Type.Contains("raw")  ? "#F59E0B"
                    : "#94A3B8";
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
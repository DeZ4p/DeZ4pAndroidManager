// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System.Windows.Media;
namespace DeZ4pAndroidManager.Models;

public class PrivacyCheck
{
    public string Title { get; set; } = "";
    public string Value { get; set; } = "";
    public string Hint { get; set; } = "";
    public string Icon { get; set; } = "\uE72E";
    public string Level { get; set; } = "info";

    public Brush LevelBrush
    {
        get
        {
            var hex = Level switch { "good" => "#34D399", "warn" => "#F59E0B", "bad" => "#EF4444", _ => "#4F8CFF" };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze(); return b;
        }
    }
}
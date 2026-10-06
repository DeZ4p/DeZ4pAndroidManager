// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

public class SensorInfo
{
    public int Handle { get; set; }
    public string Name { get; set; } = "";
    public string Vendor { get; set; } = "";
    public int Version { get; set; }
    public string TypeName { get; set; } = "";
    public int TypeId { get; set; }
    public string MaxRange { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string Power { get; set; } = "";
    public string MinDelay { get; set; } = "";
    public string FifoMax { get; set; } = "";
    public string RawLine { get; set; } = "";

    public string TypeShort
    {
        get
        {
            if (string.IsNullOrEmpty(TypeName)) return "";
            var idx = TypeName.LastIndexOf('.');
            return idx >= 0 ? TypeName.Substring(idx + 1) : TypeName;
        }
    }

    public string Icon => TypeShort switch
    {
        "accelerometer" => "\uE945",
        "gyroscope" => "\uE9D9",
        "magnetic-field" or "magnetic_field" => "\uE707",
        "proximity" => "\uE8A1",
        "light" => "\uE706",
        "pressure" => "\uE9D9",
        "step-counter" or "step_detector" => "\uE805",
        "heart-rate" or "heart_rate" => "\uEB51",
        "orientation" => "\uE7F4",
        "gravity" => "\uE9D9",
        "linear-acceleration" or "linear_acceleration" => "\uE945",
        "rotation-vector" or "rotation_vector" => "\uE9D9",
        "game-rotation-vector" or "game_rotation_vector" => "\uE7FC",
        "temperature" => "\uE9D9",
        "humidity" => "\uE9D9",
        "ambient-temperature" or "ambient_temperature" => "\uE9D9",
        "significant-motion" or "significant_motion" => "\uE805",
        "hinge-angle" or "hinge_angle" => "\uE9D9",
        _ => "\uE9D9"
    };

    public Brush TypeColor
    {
        get
        {
            var hex = TypeShort switch
            {
                "accelerometer" => "#4F8CFF",
                "gyroscope" => "#A855F7",
                "magnetic-field" or "magnetic_field" => "#EC4899",
                "proximity" => "#34D399",
                "light" => "#FBBF24",
                "pressure" => "#22D3EE",
                "step-counter" or "step_detector" => "#F59E0B",
                "heart-rate" or "heart_rate" => "#EF4444",
                "orientation" => "#818CF8",
                "gravity" => "#60A5FA",
                "temperature" or "ambient-temperature" or "ambient_temperature" => "#F97316",
                _ => "#94A3B8"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

public class LogEntry
{
    public string Raw { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public int Pid { get; set; }
    public int Tid { get; set; }
    public string Level { get; set; } = "V";   // V D I W E F
    public string Tag { get; set; } = "";
    public string Message { get; set; } = "";

    public string TimeDisplay => Time.ToString("HH:mm:ss.fff");
    public string PidDisplay => $"{Pid,6}";

    public string LevelDisplay => Level switch
    {
        "V" => "VERBOSE",
        "D" => "DEBUG",
        "I" => "INFO",
        "W" => "WARN",
        "E" => "ERROR",
        "F" => "FATAL",
        "A" => "ASSERT",
        _ => Level
    };

    public Brush LevelColor
    {
        get
        {
            var hex = Level switch
            {
                "V" => "#94A3B8",
                "D" => "#4F8CFF",
                "I" => "#34D399",
                "W" => "#F59E0B",
                "E" => "#EF4444",
                "F" => "#DC2626",
                "A" => "#DC2626",
                _ => "#E8EAED"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    public Brush TagColor
    {
        get
        {
            var hex = Level switch
            {
                "V" => "#64748B",
                "D" => "#60A5FA",
                "I" => "#22D3EE",
                "W" => "#FBBF24",
                "E" => "#F87171",
                "F" => "#F87171",
                _ => "#9AA0A6"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    public Brush MsgColor
    {
        get
        {
            var hex = Level switch
            {
                "E" or "F" => "#FCA5A5",
                "W" => "#FCD34D",
                "I" => "#E8EAED",
                "D" => "#CBD5E1",
                "V" => "#94A3B8",
                _ => "#E8EAED"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
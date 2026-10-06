// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

public enum ConsoleEntryKind { Command, Output, Error, Info, Success }

public class ConsoleEntry
{
    public ConsoleEntryKind Kind { get; set; } = ConsoleEntryKind.Output;
    public string Text { get; set; } = "";
    public DateTime Time { get; set; } = DateTime.Now;
    public long DurationMs { get; set; }

    public string TimeDisplay => Time.ToString("HH:mm:ss.fff");

    public string Prefix => Kind switch
    {
        ConsoleEntryKind.Command => "$",
        ConsoleEntryKind.Error => "!",
        ConsoleEntryKind.Info => "i",
        ConsoleEntryKind.Success => "+",
        _ => " "
    };

    public Brush TextColor
    {
        get
        {
            var hex = Kind switch
            {
                ConsoleEntryKind.Command => "#4F8CFF",
                ConsoleEntryKind.Error => "#EF4444",
                ConsoleEntryKind.Info => "#94A3B8",
                ConsoleEntryKind.Success => "#34D399",
                _ => "#E8EAED"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    public Brush PrefixColor
    {
        get
        {
            var hex = Kind switch
            {
                ConsoleEntryKind.Command => "#4F8CFF",
                ConsoleEntryKind.Error => "#EF4444",
                ConsoleEntryKind.Info => "#94A3B8",
                ConsoleEntryKind.Success => "#34D399",
                _ => "#6B7280"
            };
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}

public class PresetCommand
{
    public string Label { get; set; } = "";
    public string Command { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
}
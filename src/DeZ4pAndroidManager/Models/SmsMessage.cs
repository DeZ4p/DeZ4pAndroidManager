// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// One SMS message from the device's SMS provider.
/// </summary>
public class SmsMessage
{
    public string Id { get; set; } = "";
    public string Address { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.MinValue;
    public bool IsIncoming { get; set; }
    public bool IsRead { get; set; }

    public string DateDisplay
    {
        get
        {
            if (Date == DateTime.MinValue) return "";
            var d = Date;
            var today = DateTime.Today;
            if (d.Date == today) return d.ToString("HH:mm");
            if (d.Date == today.AddDays(-1)) return "Yesterday";
            if ((today - d.Date).TotalDays < 7) return d.ToString("ddd HH:mm");
            return d.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
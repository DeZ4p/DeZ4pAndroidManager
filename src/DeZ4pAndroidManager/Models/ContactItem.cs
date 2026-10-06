// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace DeZ4pAndroidManager.Models;

/// <summary>
/// One contact entry from the device's contacts provider.
/// </summary>
public class ContactItem
{
    public string ContactId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> PhoneNumbers { get; set; } = new();
    public List<string> Emails { get; set; } = new();

    public string PrimaryPhone => PhoneNumbers.FirstOrDefault() ?? "-";
    public string PrimaryEmail => Emails.FirstOrDefault() ?? "-";
    public string PhoneList => PhoneNumbers.Count == 0 ? "-" : string.Join(" - ", PhoneNumbers);
    public string EmailList => Emails.Count == 0 ? "-" : string.Join(" - ", Emails);

    public string Initial
    {
        get
        {
            var n = (DisplayName ?? "").Trim();
            if (n.Length > 0) return n.Substring(0, 1).ToUpperInvariant();
            return "?";
        }
    }

    private static readonly string[] Palette =
    {
        "#4F8CFF", "#34D399", "#F59E0B", "#EC4899",
        "#A855F7", "#22D3EE", "#EF4444", "#818CF8"
    };

    public Brush AvatarBrush
    {
        get
        {
            int h = Math.Abs((DisplayName ?? "").GetHashCode());
            var hex = Palette[h % Palette.Length];
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
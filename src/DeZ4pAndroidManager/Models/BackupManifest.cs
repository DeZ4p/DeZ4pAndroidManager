// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeZ4pAndroidManager.Models;

/// <summary>Backup metadata written as manifest.json in each backup folder.</summary>
public class BackupManifest
{
    public string Version { get; set; } = "1.0";
    public string CreatedAt { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DeviceSerial { get; set; } = "";
    public string AndroidVersion { get; set; } = "";
    public string BackupName { get; set; } = "";

    public Dictionary<string, CategoryInfo> Categories { get; set; } = new();

    // ═══ Runtime helpers ═══
    [JsonIgnore] public long TotalBytes { get; set; }
    [JsonIgnore] public int TotalItems { get; set; }
    [JsonIgnore] public DateTime CreatedAtDate =>
        DateTime.TryParse(CreatedAt, out var dt) ? dt : DateTime.MinValue;
    [JsonIgnore] public string SizeDisplay => MediaItem.FormatSize(TotalBytes);
    [JsonIgnore] public string CreatedDisplay =>
        CreatedAtDate == DateTime.MinValue ? "-" : CreatedAtDate.ToString("yyyy-MM-dd HH:mm");

    public static string ManifestFileName => "manifest.json";

    public void ComputeTotals()
    {
        TotalBytes = 0;
        TotalItems = 0;
        foreach (var c in Categories.Values)
        {
            TotalBytes += c.Bytes;
            TotalItems += c.Count;
        }
    }

    public string? Save(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, ManifestFileName);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return path;
        }
        catch { return null; }
    }

    public static BackupManifest? Load(string folder)
    {
        try
        {
            var path = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var m = JsonSerializer.Deserialize<BackupManifest>(json);
            m?.ComputeTotals();
            return m;
        }
        catch { return null; }
    }
}

public class CategoryInfo
{
    public int Count { get; set; }
    public long Bytes { get; set; }
    public string File { get; set; } = "";       // for single-file categories
    public string Folder { get; set; } = "";     // for folder categories
}
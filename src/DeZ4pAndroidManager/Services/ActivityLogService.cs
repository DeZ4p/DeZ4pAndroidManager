// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Persistent activity feed + statistics.
/// Stores up to 500 entries on disk; exposes the latest 50 to the UI.
/// </summary>
public class ActivityLogService
{
    private const int MaxStored = 500;
    private const int VisibleCount = 50;

    private readonly object _lock = new();
    private readonly string _storageDir;
    private readonly string _storagePath;
    private readonly List<ActivityEntry> _all = new();

    public ObservableCollection<ActivityEntry> Entries { get; } = new();

    public event EventHandler? StatsChanged;

    public ActivityLogService()
    {
        _storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeZ4pAndroidManager");
        Directory.CreateDirectory(_storageDir);
        _storagePath = Path.Combine(_storageDir, "activity.json");
        Load();
    }

    // ─── Stats ───
    public int TotalInstalls => _all.Count(e => e.Kind == "install");
    public int TodayInstalls => _all.Count(e => e.Kind == "install" && e.Timestamp.Date == DateTime.Today);

    public int TotalBackups => _all.Count(e => e.Kind == "backup");
    public int TodayBackups => _all.Count(e => e.Kind == "backup" && e.Timestamp.Date == DateTime.Today);

    public int TotalReboots => _all.Count(e => e.Kind == "reboot");
    public int TotalScreenshots => _all.Count(e => e.Kind == "screenshot");

    // ─── Log API ───
    public void LogInstall(string message)   => Add("install",    "📦", message, "#34D399");
    public void LogBackup(string message)    => Add("backup",     "💾", message, "#A855F7");
    public void LogReboot(string message)    => Add("reboot",     "🔄", message, "#EF4444");
    public void LogScreenshot(string message)=> Add("screenshot", "📸", message, "#EC4899");
    public void LogInfo(string message, string icon = "ℹ️", string color = "#4F8CFF")
        => Add("info", icon, message, color);

    public void Clear()
    {
        lock (_lock) _all.Clear();

        var dispatcher = Application.Current?.Dispatcher;
        void ClearUI() => Entries.Clear();

        if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.Invoke(ClearUI);
        else ClearUI();

        Save();
        StatsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ─── Internal ───
    private void Add(string kind, string icon, string message, string color)
    {
        var entry = new ActivityEntry
        {
            Kind = kind,
            Icon = icon,
            Message = message,
            AccentHex = color,
            Timestamp = DateTime.Now
        };

        lock (_lock)
        {
            _all.Insert(0, entry);
            while (_all.Count > MaxStored) _all.RemoveAt(_all.Count - 1);
        }

        var dispatcher = Application.Current?.Dispatcher;
        void AppendUI()
        {
            Entries.Insert(0, entry);
            while (Entries.Count > VisibleCount) Entries.RemoveAt(Entries.Count - 1);
        }

        if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.Invoke(AppendUI);
        else AppendUI();

        Save();
        StatsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        try
        {
            string json;
            lock (_lock) json = JsonSerializer.Serialize(_all);
            File.WriteAllText(_storagePath, json);
        }
        catch { /* silent - non-critical */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storagePath)) return;

            var json = File.ReadAllText(_storagePath);
            var loaded = JsonSerializer.Deserialize<List<ActivityEntry>>(json) ?? new List<ActivityEntry>();

            lock (_lock)
            {
                _all.Clear();
                _all.AddRange(loaded);
            }

            foreach (var e in _all.Take(VisibleCount)) Entries.Add(e);
        }
        catch { /* corrupt file - ignore */ }
    }
}
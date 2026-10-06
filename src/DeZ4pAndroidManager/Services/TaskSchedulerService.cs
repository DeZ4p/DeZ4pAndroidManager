// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

public class TaskSchedulerService
{
    private readonly AdbService _adb;
    private DispatcherTimer? _timer;
    private string _currentSerial = "";

    public TaskSchedulerService(AdbService adb) => _adb = adb;

    public ObservableCollection<ScheduledTask> Tasks { get; } = new();
    public event EventHandler<ScheduledTask>? TaskExecuted;

    public void Start(string serial)
    {
        _currentSerial = serial;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
    }

    public void Stop() { _timer?.Stop(); _timer = null; }

    private async Task TickAsync()
    {
        if (string.IsNullOrEmpty(_currentSerial)) return;
        var now = DateTime.Now;
        foreach (var t in Tasks)
        {
            if (!t.IsEnabled) continue;
            if ((now - t.LastRun).TotalSeconds < t.IntervalSeconds) continue;
            t.LastRun = now;
            try
            {
                var r = await _adb.ExecuteRawAsync($"-s {_currentSerial} shell {t.Command}", 20000);
                t.LastResult = r.ExitCode == 0
                    ? (string.IsNullOrEmpty(r.StandardOutput) ? "OK" : r.StandardOutput.Trim().Split('\n')[0])
                    : $"Exit {r.ExitCode}";
            }
            catch (Exception ex) { t.LastResult = ex.Message; }
            TaskExecuted?.Invoke(this, t);
        }
    }

    private static string StoragePath =>
        Path.Combine(AppPaths.Root, "tasks.json");

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StoragePath, json);
        }
        catch { }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return;
            var json = File.ReadAllText(StoragePath);
            var items = JsonSerializer.Deserialize<ScheduledTask[]>(json);
            if (items == null) return;
            Tasks.Clear();
            foreach (var t in items) Tasks.Add(t);
        }
        catch { }
    }
}
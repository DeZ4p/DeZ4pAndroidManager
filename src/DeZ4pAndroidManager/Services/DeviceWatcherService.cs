// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Background poller - watches BOTH ADB and Fastboot connections every 2 seconds.
/// </summary>
public class DeviceWatcherService
{
    private readonly AdbService _adb;
    private readonly FastbootService _fastboot;
    private readonly DeviceStateService _deviceState;

    private DispatcherTimer? _timer;
    private bool _isPolling;
    private HashSet<string> _knownSerials = new();

    public DeviceWatcherService(AdbService adb, FastbootService fastboot, DeviceStateService state)
    {
        _adb = adb;
        _fastboot = fastboot;
        _deviceState = state;
    }

    public event EventHandler<DeviceModel>? DeviceConnected;
    public event EventHandler? DeviceDisconnected;
    public event EventHandler? DevicesChanged;

    /// <summary>The full list of currently-detected devices (ADB + fastboot).</summary>
    public List<DeviceModel> CurrentDevices { get; private set; } = new();

    public void Start()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();
        _ = PollAsync();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public Task ForceCheckAsync() => PollAsync();

    private async Task PollAsync()
    {
        if (_isPolling) return;
        _isPolling = true;

        try
        {
            var all = new List<DeviceModel>();

            // ─── ADB side ───
            if (_adb.IsAdbAvailable)
            {
                try
                {
                    var adbDevices = await _adb.GetDevicesAsync();
                    foreach (var d in adbDevices) d.Mode = "adb";
                    all.AddRange(adbDevices);
                }
                catch { /* silent */ }
            }

            // ─── Fastboot side ───
            if (_fastboot.IsFastbootAvailable)
            {
                try
                {
                    var fbDevices = await _fastboot.GetDevicesAsync();
                    // Only add if the serial isn't already in ADB list
                    foreach (var d in fbDevices)
                    {
                        if (!all.Any(a => a.Serial == d.Serial))
                            all.Add(d);
                    }
                }
                catch { /* silent */ }
            }

            CurrentDevices = all;

            // ─── Determine primary device (prefer ADB-ready) ───
            var ready = all.Where(d => d.IsReady).ToList();
            var primary = ready.FirstOrDefault(d => !d.IsFastboot)
                       ?? ready.FirstOrDefault();

            var newSerials = ready.Select(d => d.Serial).ToHashSet();
            bool setChanged = !_knownSerials.SetEquals(newSerials);

            if (setChanged)
            {
                _knownSerials = newSerials;
                bool wasConnected = _deviceState.HasDevice;
                _deviceState.CurrentDevice = primary;

                if (primary != null && !wasConnected)
                    DeviceConnected?.Invoke(this, primary);
                else if (primary == null && wasConnected)
                    DeviceDisconnected?.Invoke(this, EventArgs.Empty);

                DevicesChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (primary != null)
            {
                _deviceState.CurrentDevice = primary;
            }
        }
        catch { /* silent */ }
        finally { _isPolling = false; }
    }
}
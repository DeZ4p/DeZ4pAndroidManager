// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Threading;
using System.Threading.Tasks;
namespace DeZ4pAndroidManager.Services;

public class BootloaderService
{
    private readonly FastbootService _fb;
    public BootloaderService(FastbootService fb) => _fb = fb;

    public async Task<(bool ok, string state)> GetUnlockStateAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var v = await _fb.GetVarAsync(serial, "unlocked", ct) ?? "";
            if (string.IsNullOrEmpty(v)) v = await _fb.GetVarAsync(serial, "oem-unlock-status", ct) ?? "";
            if (string.IsNullOrEmpty(v)) v = await _fb.GetVarAsync(serial, "device-unlocked", ct) ?? "";
            return (true, string.IsNullOrEmpty(v) ? "Unknown" : v);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> GetUnlockAbilityAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "flashing get_unlock_ability", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "?" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> UnlockAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "flashing unlock", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "Unlocked" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> LockAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "flashing lock", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "Locked" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> OemUnlockAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "oem unlock", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "OK" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> OemLockAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "oem lock", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "OK" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<string> GetDeviceInfoAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "oem device-info", ct);
            return ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
        }
        catch { return ""; }
    }
}
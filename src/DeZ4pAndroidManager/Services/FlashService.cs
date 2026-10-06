// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace DeZ4pAndroidManager.Services;

public class FlashService
{
    private readonly FastbootService _fb;
    public FlashService(FastbootService fb) => _fb = fb;

    public async Task<(bool ok, string output, long ms)> FlashAsync(
        string serial, string partition, string imagePath, bool bothSlots, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(imagePath)) return (false, "Image not found", sw.ElapsedMilliseconds);
            var cmd = bothSlots ? $"flash {partition} \"{imagePath}\" --slot=all" : $"flash {partition} \"{imagePath}\"";
            var r = await _fb.ExecuteAsync(serial, cmd, ct);
            sw.Stop();
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            bool ok = r.ExitCode == 0
                   && out_.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0
                   && out_.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0;
            return (ok, string.IsNullOrEmpty(out_) ? (ok ? "OK" : "Failed") : out_, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) { sw.Stop(); return (false, ex.Message, sw.ElapsedMilliseconds); }
    }

    public async Task<(bool ok, string msg)> WipeAsync(string serial, string partition, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, $"erase {partition}", ct);
            var out_ = ((r.StandardOutput ?? "") + " " + (r.StandardError ?? "")).Trim();
            return (r.ExitCode == 0, string.IsNullOrEmpty(out_) ? "OK" : out_);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> RebootAfterFlashAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _fb.ExecuteAsync(serial, "reboot", ct);
            return (r.ExitCode == 0, "Rebooting");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string slot)> GetCurrentSlotAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var s = await _fb.GetVarAsync(serial, "current-slot", ct);
            return (!string.IsNullOrEmpty(s), s ?? "?");
        }
        catch { return (false, "?"); }
    }
}
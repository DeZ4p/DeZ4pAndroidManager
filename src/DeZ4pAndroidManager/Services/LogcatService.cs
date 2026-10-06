// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeZ4pAndroidManager.Services;

public class LogcatService
{
    private readonly AdbService _adb;
    private Process? _process;
    private CancellationTokenSource? _cts;

    public LogcatService(AdbService adb) => _adb = adb;

    public bool IsRunning => _process != null && !SafeHasExited(_process);

    private static bool SafeHasExited(Process? p)
    {
        if (p == null) return true;
        try { return p.HasExited; } catch { return true; }
    }

    public event EventHandler<string>? LineReceived;
    public event EventHandler? ProcessExited;

    public async Task<(bool ok, string error)> StartAsync(
        string serial, string levelFilter, string tagFilter,
        CancellationToken ct = default)
    {
        if (IsRunning) return (false, "Already running");

        try
        {
            var args = new StringBuilder();
            args.Append($"-s {serial} logcat -v threadtime");

            // Add level/tag filter (e.g., "ActivityManager:I *:S")
            if (!string.IsNullOrWhiteSpace(levelFilter) || !string.IsNullOrWhiteSpace(tagFilter))
            {
                var filter = "";
                if (!string.IsNullOrWhiteSpace(tagFilter))
                    filter = $"{tagFilter}:{levelFilter} *:S";
                else
                    filter = $"*:{levelFilter}";
                args.Append($" {filter}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = _adb.AdbPath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _cts = new CancellationTokenSource();
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) LineReceived?.Invoke(this, e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) LineReceived?.Invoke(this, e.Data); };
            p.Exited += (_, _) => { ProcessExited?.Invoke(this, EventArgs.Empty); };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _process = p;

            await Task.Delay(500, ct);

            if (SafeHasExited(p))
            {
                var err = "";
                try { err = p.StandardError.ReadToEnd(); } catch { }
                _process = null;
                return (false, string.IsNullOrEmpty(err) ? "logcat exited immediately" : err.Trim());
            }

            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_process != null && !SafeHasExited(_process))
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
            }
        }
        catch { }
        finally
        {
            _process = null;
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;
        }
        await Task.CompletedTask;
    }

    public async Task ClearAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            await _adb.ExecuteRawAsync($"-s {serial} logcat -c", 5000, ct);
        }
        catch { }
    }

    public async Task<bool> SaveBufferAsync(string serial, string outputPath, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ExecuteRawAsync($"-s {serial} logcat -d -v threadtime", 30000, ct);
            if (r.ExitCode != 0) return false;
            File.WriteAllText(outputPath, r.StandardOutput ?? "", Encoding.UTF8);
            return true;
        }
        catch { return false; }
    }
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeZ4pAndroidManager.Services;

public class ScreenshotRecordOptions
{
    public string Serial { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public bool SaveAsJpg { get; set; } = true;
    public int DelaySeconds { get; set; } = 0;
    public bool AutoOpen { get; set; } = true;

    // Recording
    public int RecordWidth { get; set; } = 0;
    public int RecordHeight { get; set; } = 0;
    public int RecordBitRateMbps { get; set; } = 8;
    public int RecordTimeLimitSec { get; set; } = 180;
    public bool RecordRotate { get; set; }
    public bool RecordAudio { get; set; }
    public string RecordAudioSource { get; set; } = "mic";
}

public class ScreenshotRecordService
{
    private readonly AdbService _adb;
    private Process? _recordProcess;
    private string _remoteRecPath = "";
    private string _localRecPath = "";
    private string _currentSerial = "";

    public ScreenshotRecordService(AdbService adb) => _adb = adb;

    public bool IsRecording => _recordProcess != null && !SafeHasExited(_recordProcess);
    public string CurrentLocalPath => _localRecPath;

    private static bool SafeHasExited(Process? p)
    {
        if (p == null) return true;
        try { return p.HasExited; } catch { return true; }
    }

    // ═══════════════════════════════════════════════════════════
    //  VERSION DETECTION
    // ═══════════════════════════════════════════════════════════
    public async Task<int> GetApiLevelAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ShellAsync(serial, "getprop ro.build.version.sdk", ct);
            return int.TryParse(r?.Trim(), out var api) ? api : 0;
        }
        catch { return 0; }
    }

    public async Task<string> GetAndroidVersionAsync(string serial, CancellationToken ct = default)
    {
        try
        {
            var r = await _adb.ShellAsync(serial, "getprop ro.build.version.release", ct);
            var v = r?.Trim() ?? "";
            return string.IsNullOrEmpty(v) ? "?" : v;
        }
        catch { return "?"; }
    }

    public async Task<(int api, string version)> GetVersionInfoAsync(string serial, CancellationToken ct = default)
    {
        var api = await GetApiLevelAsync(serial, ct);
        var version = await GetAndroidVersionAsync(serial, ct);
        return (api, version);
    }

    // ═══════════════════════════════════════════════════════════
    //  SCREENSHOT
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool ok, string localPath, string error)> TakeScreenshotAsync(
        string serial, string outputFolder, bool useJpg, int delaySeconds,
        CancellationToken ct = default)
    {
        try
        {
            if (delaySeconds > 0)
                await Task.Delay(delaySeconds * 1000, ct);

            Directory.CreateDirectory(outputFolder);

            var ext = useJpg ? "jpg" : "png";
            var name = $"screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.{ext}";
            var local = Path.Combine(outputFolder, name);
            var remote = $"/sdcard/_dez4p_shot_{Guid.NewGuid():N}.png";

            await _adb.ShellAsync(serial, $"screencap -p {remote}", ct);

            var check = await _adb.ShellAsync(serial, $"ls -la {remote} 2>&1", ct);
            if (check.Contains("No such") || check.Contains("Permission denied"))
                return (false, "", "screencap failed on device");

            var pull = await _adb.ExecuteRawAsync($"-s {serial} pull \"{remote}\" \"{local}\"", 60000, ct);

            try { await _adb.ShellAsync(serial, $"rm -f {remote}", ct); } catch { }

            if (pull.ExitCode != 0 || !File.Exists(local))
                return (false, "", "pull failed");

            return (true, local, "");
        }
        catch (OperationCanceledException) { return (false, "", "cancelled"); }
        catch (Exception ex) { return (false, "", ex.Message); }
    }

    // ═══════════════════════════════════════════════════════════
    //  RECORDING
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool ok, string error)> StartRecordingAsync(
        string serial, string outputFolder, ScreenshotRecordOptions opts,
        CancellationToken ct = default)
    {
        if (IsRecording) return (false, "Already recording");

        try
        {
            Directory.CreateDirectory(outputFolder);

            var name = $"recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
            _localRecPath = Path.Combine(outputFolder, name);
            _remoteRecPath = $"/sdcard/_dez4p_rec_{Guid.NewGuid():N}.mp4";
            _currentSerial = serial;

            var args = new StringBuilder();
            if (opts.RecordWidth > 0 && opts.RecordHeight > 0)
                args.Append($"--size {opts.RecordWidth}x{opts.RecordHeight} ");
            if (opts.RecordBitRateMbps > 0)
                args.Append($"--bit-rate {opts.RecordBitRateMbps * 1000000} ");
            if (opts.RecordTimeLimitSec > 0)
                args.Append($"--time-limit {opts.RecordTimeLimitSec} ");
            if (opts.RecordRotate) args.Append("--rotate ");

            var api = await GetApiLevelAsync(serial, ct);
            if (api >= 30 && opts.RecordAudio)
            {
                var src = string.IsNullOrEmpty(opts.RecordAudioSource) ? "mic" : opts.RecordAudioSource;
                args.Append($"--audio-source {src} ");
            }

            args.Append(_remoteRecPath);

            var psi = new ProcessStartInfo
            {
                FileName = _adb.AdbPath,
                Arguments = $"-s {serial} shell screenrecord {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var p = new Process { StartInfo = psi };
            p.Start();
            _recordProcess = p;

            await Task.Delay(800);

            if (SafeHasExited(p))
            {
                var err = "";
                try { err = p.StandardError.ReadToEnd(); } catch { }
                var output = "";
                try { output = p.StandardOutput.ReadToEnd(); } catch { }
                _recordProcess = null;
                var msg = string.IsNullOrEmpty(err) ? output : err;
                return (false, string.IsNullOrEmpty(msg) ? "screenrecord exited immediately" : msg.Trim());
            }

            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string localPath, string error)> StopRecordingAsync(
        CancellationToken ct = default)
    {
        if (_recordProcess == null)
            return (false, "", "Not recording");

        var serial = _currentSerial;
        var remote = _remoteRecPath;
        var local = _localRecPath;
        var proc = _recordProcess;
        _recordProcess = null;

        try
        {
            try
            {
                await _adb.ShellAsync(serial,
                    "pkill -INT screenrecord 2>/dev/null; kill -2 $(pidof screenrecord) 2>/dev/null", ct);
            }
            catch { }

            try { if (!SafeHasExited(proc)) proc.Kill(entireProcessTree: true); } catch { }
            try { proc.Dispose(); } catch { }

            await Task.Delay(2500);

            var check = await _adb.ShellAsync(serial, $"ls -la {remote} 2>&1", ct);
            if (check.Contains("No such"))
                return (false, "", "Recording file not found on device");

            var pull = await _adb.ExecuteRawAsync($"-s {serial} pull \"{remote}\" \"{local}\"", 300000, ct);

            try { await _adb.ShellAsync(serial, $"rm -f {remote}", ct); } catch { }

            if (pull.ExitCode != 0 || !File.Exists(local))
                return (false, "", "pull failed");

            return (true, local, "");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }
}
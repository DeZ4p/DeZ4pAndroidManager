// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DeZ4pAndroidManager.Services;

public class ScreenMirrorOptions
{
    public string Serial { get; set; } = "";
    public int MaxSize { get; set; } = 1024;
    public int VideoBitRateMbps { get; set; } = 8;
    public int MaxFps { get; set; } = 60;
    public bool Fullscreen { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool TurnScreenOff { get; set; }
    public bool StayAwake { get; set; }
    public bool ShowTouches { get; set; }
    public bool NoAudio { get; set; } = true;
    public bool AudioOnly { get; set; }
    public bool Record { get; set; }
    public string RecordPath { get; set; } = "";
    public string WindowTitle { get; set; } = "DeZ4p Mirror";
    public string VideoCodec { get; set; } = "";
    public string KeyboardMode { get; set; } = "";
    public string Crop { get; set; } = "";
}

public class ScreenMirrorService
{
    private readonly AdbService _adb;
    private Process? _currentProcess;
    private string? _cachedVersion;

    public ScreenMirrorService(AdbService adb) => _adb = adb;

    public string ScrcpyPath
    {
        get
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var direct = new[]
            {
                Path.Combine(baseDir, "tools", "scrcpy", "scrcpy.exe"),
                Path.Combine(baseDir, "tools", "platform-tools", "scrcpy.exe"),
                Path.Combine(baseDir, "tools", "scrcpy.exe"),
            };
            foreach (var p in direct) if (File.Exists(p)) return p;

            try
            {
                var root = Path.Combine(baseDir, "tools");
                if (Directory.Exists(root))
                {
                    var found = Directory.GetFiles(root, "scrcpy.exe", SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }
            }
            catch { }

            return Path.Combine(baseDir, "tools", "scrcpy", "scrcpy.exe");
        }
    }

    public string AdbPath => _adb.AdbPath;
    public bool IsScrcpyAvailable => File.Exists(ScrcpyPath);
    public bool IsRunning => _currentProcess != null && !SafeHasExited(_currentProcess);

    private static bool SafeHasExited(Process? p)
    {
        if (p == null) return true;
        try { return p.HasExited; } catch { return true; }
    }

    public async Task<string> GetScrcpyVersionAsync()
    {
        if (!IsScrcpyAvailable) return "not found";
        try
        {
            var (code, output) = await RunProcessAsync(ScrcpyPath, "--version", 5000);
            var first = (output ?? "").Split('\n')[0].Trim();
            _cachedVersion = ExtractVersionNumber(first);
            return string.IsNullOrEmpty(first) ? "unknown" : first;
        }
        catch (Exception ex) { return $"error: {ex.Message}"; }
    }

    public async Task<string> GetVersionNumberAsync()
    {
        if (_cachedVersion != null) return _cachedVersion;
        await GetScrcpyVersionAsync();
        return _cachedVersion ?? "";
    }

    private static string ExtractVersionNumber(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var m = Regex.Match(s, @"(\d+\.\d+(?:\.\d+)?)");
        return m.Success ? m.Groups[1].Value : "";
    }

    private static int MajorVersion(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        var parts = v.Split('.');
        return int.TryParse(parts[0], out var n) ? n : 0;
    }

    /// <summary>
    /// Builds scrcpy args. Uses ADB env var (not --adb=...) because Windows
    /// paths with backslashes confuse scrcpy's argument parser.
    /// </summary>
    public string BuildArguments(ScreenMirrorOptions o, string version)
    {
        int major = MajorVersion(version);
        bool isV2Plus = major >= 2;

        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(o.Serial)) sb.Append($"-s {o.Serial} ");

        if (o.MaxSize > 0) sb.Append($"--max-size={o.MaxSize} ");
        if (o.MaxFps > 0) sb.Append($"--max-fps={o.MaxFps} ");

        if (o.VideoBitRateMbps > 0)
        {
            if (isV2Plus)
                sb.Append($"--video-bit-rate={o.VideoBitRateMbps}M ");
            else
                sb.Append($"--bit-rate={o.VideoBitRateMbps}M ");
        }

        if (isV2Plus && !string.IsNullOrEmpty(o.VideoCodec))
            sb.Append($"--video-codec={o.VideoCodec} ");

        if (o.Fullscreen) sb.Append("--fullscreen ");
        if (o.AlwaysOnTop) sb.Append("--always-on-top ");
        if (!string.IsNullOrEmpty(o.WindowTitle))
            sb.Append($"--window-title=\"{o.WindowTitle}\" ");

        if (o.TurnScreenOff) sb.Append("--turn-screen-off ");
        if (o.StayAwake) sb.Append("--stay-awake ");
        if (o.ShowTouches) sb.Append("--show-touches ");

        if (!string.IsNullOrEmpty(o.KeyboardMode) && isV2Plus)
            sb.Append($"--keyboard={o.KeyboardMode} ");

        if (!string.IsNullOrEmpty(o.Crop))
            sb.Append($"--crop={o.Crop} ");

        if (isV2Plus)
        {
            if (o.NoAudio) sb.Append("--no-audio ");
            else if (o.AudioOnly) sb.Append("--no-video ");
        }

        if (o.Record && !string.IsNullOrEmpty(o.RecordPath))
            sb.Append($"--record=\"{o.RecordPath}\" ");

        return sb.ToString().Trim();
    }

    public event EventHandler<string>? OutputReceived;
    public event EventHandler? MirrorStarted;
    public event EventHandler<int>? MirrorExited;

    public async Task<(bool ok, string error)> StartWithFallbackAsync(ScreenMirrorOptions options)
    {
        var version = await GetVersionNumberAsync();

        var fullArgs = BuildArguments(options, version);
        var result = await TryStartAndVerifyAsync(fullArgs);
        if (result.ok) return (true, "");

        // Minimal retry: serial only
        var miniArgs = string.IsNullOrEmpty(options.Serial) ? "" : $"-s {options.Serial}";
        if (!string.Equals(miniArgs, fullArgs, StringComparison.Ordinal))
        {
            OutputReceived?.Invoke(this, "");
            OutputReceived?.Invoke(this, "═══════════════════════════════════════");
            OutputReceived?.Invoke(this, "Full args failed. Retrying with MINIMAL args...");
            OutputReceived?.Invoke(this, $"Minimal args: {miniArgs}");
            OutputReceived?.Invoke(this, "═══════════════════════════════════════");
            var result2 = await TryStartAndVerifyAsync(miniArgs);
            if (result2.ok) return (true, "");
            return (false, result2.error);
        }

        return (false, result.error);
    }

    private async Task<(bool ok, string error)> TryStartAndVerifyAsync(string args)
    {
        var workDir = Path.GetDirectoryName(ScrcpyPath) ?? "";

        var psi = new ProcessStartInfo
        {
            FileName = ScrcpyPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workDir
        };

        // ⭐ Set ADB env var - this avoids the Windows-path-parsing bug
        try
        {
            var adbPath = _adb.AdbPath;
            if (!string.IsNullOrEmpty(adbPath) && File.Exists(adbPath))
            {
                psi.EnvironmentVariables["ADB"] = adbPath;
            }

            // Add platform-tools folder to PATH (null-safe, StringDictionary-compatible)
            var adbDir = string.IsNullOrEmpty(adbPath) ? "" : Path.GetDirectoryName(adbPath);
            if (!string.IsNullOrEmpty(adbDir) && Directory.Exists(adbDir))
            {
                // StringDictionary.ContainsKey works; TryGetValue does NOT exist on it
                string existing = "";
                if (psi.EnvironmentVariables.ContainsKey("PATH"))
                {
                    var pv = psi.EnvironmentVariables["PATH"];
                    if (!string.IsNullOrEmpty(pv)) existing = pv;
                }
                if (existing.Length == 0)
                {
                    existing = Environment.GetEnvironmentVariable("PATH") ?? "";
                }

                bool alreadyPresent = existing.Length > 0 &&
                    existing.IndexOf(adbDir, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!alreadyPresent)
                {
                    psi.EnvironmentVariables["PATH"] = adbDir + ";" + existing;
                }
            }
        }
        catch { }

        OutputReceived?.Invoke(this, "");
        OutputReceived?.Invoke(this, $"▶ scrcpy {args}");
        OutputReceived?.Invoke(this, $"  (ADB={_adb.AdbPath})");

        var capturedOutput = new StringBuilder();
        bool hasExited = false;
        int exitCode = -1;

        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var outputLock = new object();

        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (outputLock) capturedOutput.AppendLine(e.Data);
            OutputReceived?.Invoke(this, e.Data);
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (outputLock) capturedOutput.AppendLine(e.Data);
            OutputReceived?.Invoke(this, e.Data);
        };
        p.Exited += (_, _) =>
        {
            hasExited = true;
            try { exitCode = p.ExitCode; } catch { }
            MirrorExited?.Invoke(this, exitCode);
        };

        try
        {
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            try { p.Dispose(); } catch { }
            return (false, $"Failed to launch scrcpy:\n{ex.Message}");
        }

        await Task.Delay(1500);

        if (hasExited)
        {
            string err;
            lock (outputLock) err = capturedOutput.ToString().Trim();
            if (string.IsNullOrEmpty(err)) err = $"(no output captured, exit code {exitCode})";
            try { p.Dispose(); } catch { }
            return (false, $"scrcpy exited immediately (code {exitCode}).\n\n{err}");
        }

        _currentProcess = p;
        MirrorStarted?.Invoke(this, EventArgs.Empty);
        return (true, "");
    }

    public void Stop()
    {
        var p = _currentProcess;
        _currentProcess = null;
        if (p == null) return;
        try { if (!SafeHasExited(p)) p.Kill(entireProcessTree: true); } catch { }
        try { p.Dispose(); } catch { }
    }

    public void OpenScrcpyFolder()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var folder = Path.Combine(baseDir, "tools", "scrcpy");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch { }
    }

    private static async Task<(int code, string output)> RunProcessAsync(string exe, string args, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
        };

        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeoutMs);
        try { await p.WaitForExitAsync(cts.Token); }
        catch { try { p.Kill(true); } catch { } }

        int code = -1;
        try { code = p.HasExited ? p.ExitCode : -1; } catch { }
        return (code, sb.ToString());
    }
}
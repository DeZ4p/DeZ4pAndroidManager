// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Core ADB service. Works with ADB 1.0.36 through 1.0.41+ and
/// handles all Android 7 (API 24) → Android 16 (API 36) devices.
/// Every command has a timeout and returns structured results.
/// </summary>
public class AdbService
{
    private const int DefaultTimeoutMs = 10000;
    private const int LongRunningTimeoutMs = 120000; // installs / big pulls

    private readonly string _adbPath;

    public AdbService()
    {
        // Universal path resolution - works from any working directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _adbPath = Path.Combine(baseDir, "tools", "platform-tools",
                                OperatingSystem.IsWindows() ? "adb.exe" : "adb");
    }

    public string AdbPath => _adbPath;
    public bool IsAdbAvailable => File.Exists(_adbPath);

    /// <summary>
    /// Cached ADB version (populated on first version query).
    /// </summary>
    public string AdbVersion { get; private set; } = "unknown";

    /// <summary>
    /// True if the ADB version supports install-multiple (1.0.40+).
    /// </summary>
    public bool SupportsInstallMultiple { get; private set; }

    /// <summary>
    /// Ensures the ADB server is running. Safe to call multiple times.
    /// </summary>
    public async Task<bool> StartServerAsync(CancellationToken ct = default)
    {
        var result = await ExecuteAsync("start-server", ct).ConfigureAwait(false);
        await DetectVersionAsync(ct).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Detects ADB version and capabilities. Called automatically.
    /// </summary>
    private async Task DetectVersionAsync(CancellationToken ct)
    {
        if (AdbVersion != "unknown") return;
        var v = await ExecuteAsync("version", ct).ConfigureAwait(false);
        var line = v.StandardOutput.Split('\n')[0].Trim();
        // Example: "Android Debug Bridge version 1.0.41"
        var idx = line.IndexOf("version", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            AdbVersion = line.Substring(idx + 7).Trim();
            // install-multiple exists in 1.0.40+
            SupportsInstallMultiple = AdbVersion.CompareTo("1.0.40") >= 0;
        }
    }

    /// <summary>
    /// Gets the list of devices currently visible to ADB.
    /// Handles device, offline, unauthorized, bootloader, recovery states.
    /// </summary>
    public async Task<List<DeviceModel>> GetDevicesAsync(CancellationToken ct = default)
    {
        var result = await ExecuteAsync("devices -l", ct).ConfigureAwait(false);
        var devices = new List<DeviceModel>();

        foreach (var rawLine in result.StandardOutput.Split('\n'))
        {
            string line = rawLine.Replace("\r", "").Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("List of devices")) continue;
            if (line.StartsWith("*")) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var device = new DeviceModel { Serial = parts[0], State = parts[1] };

            for (int i = 2; i < parts.Length; i++)
            {
                var kv = parts[i].Split(':', 2);
                if (kv.Length != 2) continue;
                switch (kv[0])
                {
                    case "model": device.Model = kv[1].Replace('_', ' '); break;
                    case "product": device.Product = kv[1]; break;
                    case "device": device.Device = kv[1]; break;
                    case "transport_id": device.TransportId = kv[1]; break;
                }
            }
            devices.Add(device);
        }
        return devices;
    }

    /// <summary>
    /// Runs an ADB shell command on the specified device.
    /// </summary>
    public async Task<string> ShellAsync(string serial, string command, CancellationToken ct = default)
    {
        var result = await ExecuteAsync($"-s {serial} shell {command}", ct).ConfigureAwait(false);
        return result.StandardOutput;
    }

    /// <summary>
    /// Executes a raw adb command with the default timeout.
    /// </summary>
    public Task<RawResult> ExecuteRawAsync(string arguments, CancellationToken ct = default)
        => ExecuteRawAsync(arguments, DefaultTimeoutMs, ct);

    /// <summary>
    /// Executes a raw adb command with a custom timeout.
    /// </summary>
    public async Task<RawResult> ExecuteRawAsync(string arguments, int timeoutMs, CancellationToken ct = default)
    {
        var r = await ExecuteAsync(arguments, ct, timeoutMs).ConfigureAwait(false);
        return new RawResult(r.ExitCode, r.StandardOutput, r.StandardError);
    }

    /// <summary>
    /// Installs a single APK. Works with any ADB version.
    /// </summary>
    public async Task<InstallResult> InstallApkAsync(
        string serial, string apkPath,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(apkPath))
            return new InstallResult(false, $"APK file not found: {apkPath}");

        var args = $"-s {serial} install -r \"{apkPath}\"";
        var result = await ExecuteWithLiveOutputAsync(args, progress, ct, LongRunningTimeoutMs).ConfigureAwait(false);
        return BuildInstallResult(result);
    }

    /// <summary>
    /// Installs split APKs via install-multiple.
    /// Falls back to sequential installs if ADB is too old.
    /// </summary>
    public async Task<InstallResult> InstallSplitApkAsync(
        string serial, IEnumerable<string> apkPaths,
        IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var paths = apkPaths.Where(File.Exists).ToList();
        if (paths.Count == 0)
            return new InstallResult(false, "No valid APK files to install.");

        if (SupportsInstallMultiple)
        {
            var quoted = string.Join(" ", paths.Select(p => $"\"{p}\""));
            var args = $"-s {serial} install-multiple -r {quoted}";
            var result = await ExecuteWithLiveOutputAsync(args, progress, ct, LongRunningTimeoutMs).ConfigureAwait(false);
            return BuildInstallResult(result);
        }

        // Fallback for ADB 1.0.39 and older - install one by one
        progress?.Report("⚠ ADB too old for install-multiple - installing sequentially");
        var allOutput = new StringBuilder();
        bool allOk = true;

        foreach (var path in paths)
        {
            var args = $"-s {serial} install -r \"{path}\"";
            var r = await ExecuteWithLiveOutputAsync(args, progress, ct, LongRunningTimeoutMs).ConfigureAwait(false);
            allOutput.AppendLine(r.StandardOutput);
            if (r.ExitCode != 0) allOk = false;
        }

        return new InstallResult(allOk, allOutput.ToString().Trim());
    }

    /// <summary>
    /// Pulls a file/folder from the device.
    /// </summary>
    public async Task<RawResult> PullAsync(string serial, string remotePath, string localPath, CancellationToken ct = default)
        => await ExecuteRawAsync($"-s {serial} pull \"{remotePath}\" \"{localPath}\"", LongRunningTimeoutMs, ct);

    /// <summary>
    /// Pushes a file/folder to the device.
    /// </summary>
    public async Task<RawResult> PushAsync(string serial, string localPath, string remotePath, CancellationToken ct = default)
        => await ExecuteRawAsync($"-s {serial} push \"{localPath}\" \"{remotePath}\"", LongRunningTimeoutMs, ct);

    /// <summary>
    /// Captures a screenshot. Uses the standard screencap method
    /// that works on Android 7 → Android 16 across all OEMs.
    /// </summary>
    public async Task<bool> TakeScreenshotAsync(string serial, string localSavePath, CancellationToken ct = default)
    {
        // Use unique remote name to avoid collisions
        string remote = $"/sdcard/_dez4p_shot_{System.Guid.NewGuid():N}.png";

        var cap = await ExecuteRawAsync($"-s {serial} shell screencap -p {remote}", ct);
        if (cap.ExitCode != 0) return false;

        var pull = await PullAsync(serial, remote, localSavePath, ct);

        // Best-effort cleanup
        await ExecuteRawAsync($"-s {serial} shell rm -f {remote}", ct);

        return pull.ExitCode == 0 && File.Exists(localSavePath);
    }

    private static InstallResult BuildInstallResult(ProcessResult result)
    {
        string combined = result.StandardOutput + "\n" + result.StandardError;
        bool success = combined.Contains("Success", StringComparison.OrdinalIgnoreCase)
                       && result.ExitCode == 0;
        return new InstallResult(success, combined.Trim());
    }

    private async Task<ProcessResult> ExecuteWithLiveOutputAsync(
        string arguments, IProgress<string>? progress,
        CancellationToken ct = default, int timeoutMs = DefaultTimeoutMs)
    {
        if (!IsAdbAvailable)
            return new ProcessResult(-1, string.Empty, $"adb not found at: {_adbPath}");

        var psi = BuildProcessStartInfo(arguments);
        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) { stdout.AppendLine(e.Data); progress?.Report(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) { stderr.AppendLine(e.Data); progress?.Report(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ProcessResult(-1, stdout.ToString().Trim(),
                $"Timed out after {timeoutMs}ms");
        }

        return new ProcessResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private async Task<ProcessResult> ExecuteAsync(string arguments, CancellationToken ct = default, int timeoutMs = DefaultTimeoutMs)
    {
        if (!IsAdbAvailable)
            return new ProcessResult(-1, string.Empty, $"adb not found at: {_adbPath}");

        var psi = BuildProcessStartInfo(arguments);
        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ProcessResult(-1, stdout.ToString().Trim(),
                $"Timed out after {timeoutMs}ms");
        }

        return new ProcessResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private ProcessStartInfo BuildProcessStartInfo(string arguments) => new()
    {
        FileName = _adbPath,
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        WorkingDirectory = Path.GetDirectoryName(_adbPath) ?? AppDomain.CurrentDomain.BaseDirectory
    };

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

public readonly record struct RawResult(int ExitCode, string StandardOutput, string StandardError);
public readonly record struct InstallResult(bool Success, string Output);
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Fastboot service - detects and controls devices in bootloader/fastbootd mode.
/// Uses fastboot.exe from the bundled platform-tools folder.
/// </summary>
public class FastbootService
{
    private const int DefaultTimeoutMs = 15000;
    private const int InfoTimeoutMs = 8000;

    private readonly string _fastbootPath;

    public FastbootService()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _fastbootPath = Path.Combine(baseDir, "tools", "platform-tools",
                                     OperatingSystem.IsWindows() ? "fastboot.exe" : "fastboot");
    }

    public string FastbootPath => _fastbootPath;
    public bool IsFastbootAvailable => File.Exists(_fastbootPath);

    /// <summary>
    /// Lists devices currently visible in fastboot mode.
    /// </summary>
    public async Task<List<DeviceModel>> GetDevicesAsync(CancellationToken ct = default)
    {
        var result = await ExecuteAsync("devices", ct).ConfigureAwait(false);
        var devices = new List<DeviceModel>();

        foreach (var rawLine in result.StandardOutput.Split('\n'))
        {
            string line = rawLine.Replace("\r", "").Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var device = new DeviceModel
            {
                Serial = parts[0],
                State = parts[1],
                Mode = "fastboot"
            };
            devices.Add(device);
        }

        return devices;
    }

    /// <summary>
    /// Fetches the standard info available from a device in fastboot mode.
    /// Only ~10 fields are ever available - Android System is not running.
    /// </summary>
    public async Task<FastbootInfo> GetInfoAsync(string serial, CancellationToken ct = default)
    {
        var info = new FastbootInfo { Serial = serial };

        if (!IsFastbootAvailable)
            return info;

        info.Product = await SafeGetVarAsync(serial, "product", ct) ?? "N/A";
        info.BootloaderVersion = await SafeGetVarAsync(serial, "version-bootloader", ct)
                              ?? await SafeGetVarAsync(serial, "version", ct)
                              ?? "N/A";
        info.Baseband = await SafeGetVarAsync(serial, "version-baseband", ct) ?? "N/A";
        info.Unlocked = await SafeGetVarAsync(serial, "unlocked", ct)
                     ?? await SafeGetVarAsync(serial, "oem-unlock-status", ct)
                     ?? "N/A";
        info.Secure = await SafeGetVarAsync(serial, "secure", ct) ?? "N/A";
        info.CurrentSlot = await SafeGetVarAsync(serial, "current-slot", ct)
                        ?? await SafeGetVarAsync(serial, "slot-current", ct)
                        ?? "N/A";
        info.BatteryVoltage = await SafeGetVarAsync(serial, "battery-voltage", ct) ?? "N/A";
        info.MaxDownloadSize = await SafeGetVarAsync(serial, "max-download-size", ct) ?? "N/A";
        info.PartitionType = await SafeGetVarAsync(serial, "partition-type:boot", ct) ?? "N/A";

        return info;
    }

    private async Task<string?> SafeGetVarAsync(string serial, string varName, CancellationToken ct)
    {
        try { return await GetVarAsync(serial, varName, ct).ConfigureAwait(false); }
        catch { return null; }
    }

    /// <summary>
    /// Runs a fastboot command targeting a specific device.
    /// </summary>
    public async Task<RawResult> ExecuteAsync(string serial, string command,
                                              CancellationToken ct = default)
    {
        var args = string.IsNullOrEmpty(serial) ? command : $"-s {serial} {command}";
        var r = await ExecuteAsync(args, ct).ConfigureAwait(false);
        return new RawResult(r.ExitCode, r.StandardOutput, r.StandardError);
    }

    /// <summary>
    /// Gets a specific fastboot variable (e.g., "product", "unlocked", "version").
    /// Fastboot typically prints `varName: VALUE` on stderr, but some versions
    /// use stdout - we scan both.
    /// </summary>
    public async Task<string?> GetVarAsync(string serial, string varName,
                                            CancellationToken ct = default)
    {
        var r = await ExecuteAsync($"-s {serial} getvar {varName}", ct, InfoTimeoutMs)
                    .ConfigureAwait(false);
        string combined = r.StandardOutput + "\n" + r.StandardError;

        // fastboot prints: "[varName]: VALUE" or "varName: VALUE" or on newer: just "VALUE"
        foreach (var line in combined.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            int idx = trimmed.IndexOf(varName + ":", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var value = trimmed.Substring(idx + varName.Length + 1).Trim();
            if (!string.IsNullOrEmpty(value)) return value;
        }

        return null;
    }

    // ─── Common commands ───

    public Task<RawResult> RebootAsync(string serial, CancellationToken ct = default)
        => ExecuteAsync(serial, "reboot", ct);

    public Task<RawResult> RebootBootloaderAsync(string serial, CancellationToken ct = default)
        => ExecuteAsync(serial, "reboot-bootloader", ct);

    public Task<RawResult> RebootFastbootdAsync(string serial, CancellationToken ct = default)
        => ExecuteAsync(serial, "reboot fastboot", ct);

    public Task<RawResult> RebootRecoveryAsync(string serial, CancellationToken ct = default)
        => ExecuteAsync(serial, "reboot recovery", ct);

    public Task<RawResult> ShutdownAsync(string serial, CancellationToken ct = default)
        => ExecuteAsync(serial, "oem poweroff", ct);

    // ─── Internal process runner ───

    private Task<ProcessResult> ExecuteAsync(string arguments, CancellationToken ct = default)
        => ExecuteAsync(arguments, ct, DefaultTimeoutMs);

    private async Task<ProcessResult> ExecuteAsync(string arguments, CancellationToken ct,
                                                    int timeoutMs)
    {
        if (!IsFastbootAvailable)
            return new ProcessResult(-1, string.Empty, $"fastboot not found at: {_fastbootPath}");

        var psi = new ProcessStartInfo
        {
            FileName = _fastbootPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(_fastbootPath) ?? AppDomain.CurrentDomain.BaseDirectory
        };

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
            return new ProcessResult(-1, stdout.ToString().Trim(), $"Timed out after {timeoutMs}ms");
        }

        return new ProcessResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
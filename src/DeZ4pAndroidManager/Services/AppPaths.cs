// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.IO;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Central storage manager. All output folders live under:
///   %USERPROFILE%\Documents\DeZ4p Android Manager\
///
/// Subfolders:
///   Screenshots\
///   Video Recordings\
///   Screen Mirror Recordings\
///   Device Backups\
///   APK Backups\
///   Split APK\Packed\
///   Split APK\Unpacked\
///   Logs\
///   Console Output\
///   Sensors Dumps\
///   Cache\Preview\
/// </summary>
public static class AppPaths
{
    public const string RootFolderName = "DeZ4p Android Manager";

    private static string? _root;

    /// <summary>Root folder: Documents\DeZ4p Android Manager\</summary>
    public static string Root
    {
        get
        {
            if (_root != null) return _root;

            string baseDir;
            try
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(baseDir))
                    baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(baseDir))
                    baseDir = Path.GetTempPath();
            }
            catch
            {
                baseDir = Path.GetTempPath();
            }

            _root = Path.Combine(baseDir, RootFolderName);
            try { Directory.CreateDirectory(_root); } catch { }
            return _root;
        }
    }

    // ─── Individual folders ───
    public static string Screenshots => EnsureSub("Screenshots");
    public static string VideoRecordings => EnsureSub("Video Recordings");
    public static string ScreenMirrorRecordings => EnsureSub("Screen Mirror Recordings");
    public static string DeviceBackups => EnsureSub("Device Backups");
    public static string ApkBackups => EnsureSub("APK Backups");
    public static string SplitApkPacked => EnsureSub("Split APK", "Packed");
    public static string SplitApkUnpacked => EnsureSub("Split APK", "Unpacked");
    public static string Logs => EnsureSub("Logs");
    public static string ConsoleOutput => EnsureSub("Console Output");
    public static string SensorsDumps => EnsureSub("Sensors Dumps");
    public static string CachePreview => EnsureSub("Cache", "Preview");

    private static string EnsureSub(params string[] parts)
    {
        try
        {
            var path = Root;
            foreach (var p in parts) path = Path.Combine(path, p);
            Directory.CreateDirectory(path);
            return path;
        }
        catch
        {
            var fallback = Path.Combine(Path.GetTempPath(), RootFolderName);
            foreach (var p in parts) fallback = Path.Combine(fallback, p);
            try { Directory.CreateDirectory(fallback); } catch { }
            return fallback;
        }
    }

    /// <summary>Opens the root folder in Explorer.</summary>
    public static void OpenRoot()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Root,
                UseShellExecute = true
            });
        }
        catch { }
    }

    /// <summary>Opens a specific sub-folder in Explorer.</summary>
    public static void OpenFolder(string? folder)
    {
        try
        {
            if (string.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
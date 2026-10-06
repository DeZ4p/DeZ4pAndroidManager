// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Split APK toolkit - analyze, pack, unpack, install split bundles.
/// Works with .apks files (BundleTool format) and folders of .apk splits.
/// </summary>
public class SplitApkService
{
    private readonly AdbService _adb;

    public SplitApkService(AdbService adb) => _adb = adb;

    private static readonly HashSet<string> ArchTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "arm64_v8a", "armeabi_v7a", "armeabi", "arm64", "x86", "x86_64",
        "mips", "mips64", "riscv64"
    };

    private static readonly HashSet<string> DensityTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "ldpi", "mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi", "tvdpi", "nodpi", "anydpi"
    };

    // ═══════════════════════════════════════════════════════════
    //  DETECT TYPE FROM FILENAME
    // ═══════════════════════════════════════════════════════════
    public static (SplitFileType type, string label, List<string> archs,
                   List<string> langs, List<string> densities) DetectFromName(string fileName)
    {
        var name = (fileName ?? "").ToLowerInvariant();
        var archs = new List<string>();
        var langs = new List<string>();
        var dens = new List<string>();

        if (name.EndsWith(".apk")) name = name.Substring(0, name.Length - 4);

        // split_feature_<name>
        if (name.StartsWith("split_feature_"))
        {
            var feat = name.Substring("split_feature_".Length);
            return (SplitFileType.Feature, feat, archs, langs, dens);
        }

        // split_config.<config>
        if (name.StartsWith("split_config."))
        {
            var config = name.Substring("split_config.".Length);

            if (ArchTokens.Contains(config))
            {
                archs.Add(config);
                return (SplitFileType.Native, config, archs, langs, dens);
            }

            if (DensityTokens.Contains(config))
            {
                dens.Add(config);
                return (SplitFileType.Density, config, archs, langs, dens);
            }

            if (config.Length >= 2 && config.Length <= 7)
            {
                langs.Add(config);
                return (SplitFileType.Language, config, archs, langs, dens);
            }

            return (SplitFileType.Config, config, archs, langs, dens);
        }

        // split_<something>
        if (name.StartsWith("split_"))
        {
            var rest = name.Substring("split_".Length);
            return (SplitFileType.Config, rest, archs, langs, dens);
        }

        return (SplitFileType.Base, "Base", archs, langs, dens);
    }

    // ═══════════════════════════════════════════════════════════
    //  ANALYZE
    // ═══════════════════════════════════════════════════════════
    public async Task<SplitApkSet?> AnalyzeAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!File.Exists(path) && !Directory.Exists(path)) return null;

        return await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var set = new SplitApkSet
                    {
                        SourcePath = path,
                        EffectiveFolder = path,
                        IsApksFile = false,
                        IsTemporaryFolder = false,
                        InferredName = new DirectoryInfo(path).Name
                    };

                    var apkFiles = Directory.GetFiles(path, "*.apk", SearchOption.TopDirectoryOnly)
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var apk in apkFiles)
                    {
                        ct.ThrowIfCancellationRequested();
                        var info = ReadSplitInfo(apk);
                        if (info != null)
                        {
                            if (info.Type == SplitFileType.Base) set.HasBase = true;
                            set.Splits.Add(info);
                        }
                    }

                    return set.Splits.Count > 0 ? set : null;
                }

                if (File.Exists(path))
                {
                    var set = new SplitApkSet
                    {
                        SourcePath = path,
                        IsApksFile = true,
                        IsTemporaryFolder = false,
                        InferredName = Path.GetFileNameWithoutExtension(path)
                    };

                    using var apksZip = ZipFile.OpenRead(path);
                    foreach (var entry in apksZip.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!entry.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) continue;

                        try
                        {
                            using var es = entry.Open();
                            using var ms = new MemoryStream();
                            es.CopyTo(ms);
                            ms.Position = 0;

                            using var innerZip = new ZipArchive(ms, ZipArchiveMode.Read);
                            var info = BuildInfo(entry.Name, entry.FullName, entry.Length, innerZip);
                            if (info.Type == SplitFileType.Base) set.HasBase = true;
                            set.Splits.Add(info);
                        }
                        catch
                        {
                            var (type, label, archs, langs, dens) = DetectFromName(entry.Name);
                            set.Splits.Add(new SplitFileInfo
                            {
                                FileName = entry.Name,
                                FullPath = entry.FullName,
                                SizeBytes = entry.Length,
                                Type = type,
                                TypeLabel = label,
                                Architectures = archs,
                                Languages = langs,
                                Densities = dens
                            });
                            if (type == SplitFileType.Base) set.HasBase = true;
                        }
                    }

                    return set.Splits.Count > 0 ? set : null;
                }
            }
            catch { }
            return null;
        }, ct);
    }

    private SplitFileInfo? ReadSplitInfo(string apkPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(apkPath);
            return BuildInfo(Path.GetFileName(apkPath), apkPath, new FileInfo(apkPath).Length, zip);
        }
        catch
        {
            var (type, label, archs, langs, dens) = DetectFromName(Path.GetFileName(apkPath));
            return new SplitFileInfo
            {
                FileName = Path.GetFileName(apkPath),
                FullPath = apkPath,
                SizeBytes = new FileInfo(apkPath).Length,
                Type = type,
                TypeLabel = label,
                Architectures = archs,
                Languages = langs,
                Densities = dens
            };
        }
    }

    private SplitFileInfo BuildInfo(string fileName, string fullPath, long size, ZipArchive zip)
    {
        var (type, label, archs, langs, dens) = DetectFromName(fileName);

        int dexCount = 0;
        int nativeLibCount = 0;
        var archSet = new HashSet<string>(archs, StringComparer.OrdinalIgnoreCase);

        foreach (var e in zip.Entries)
        {
            if (e.Name.EndsWith(".dex", StringComparison.OrdinalIgnoreCase)) dexCount++;

            if (e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = e.FullName.Split('/');
                if (parts.Length >= 2)
                {
                    archSet.Add(parts[1]);
                    nativeLibCount++;
                }
            }
        }

        return new SplitFileInfo
        {
            FileName = fileName,
            FullPath = fullPath,
            SizeBytes = size,
            Type = type,
            TypeLabel = label,
            Architectures = archSet.ToList(),
            Languages = langs,
            Densities = dens,
            DexCount = dexCount,
            NativeLibCount = nativeLibCount,
            TotalEntries = zip.Entries.Count
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  PACK - folder of APKs → .apks file
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool ok, string output, string error)> PackAsync(
        string sourceFolder, string outputApksPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceFolder))
            return (false, "", "Source folder not found");

        var apks = Directory.GetFiles(sourceFolder, "*.apk", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f) == "base.apk" ? 0 : 1)
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (apks.Count == 0)
            return (false, "", "No .apk files found in folder");

        return await Task.Run(() =>
        {
            try
            {
                var dir = Path.GetDirectoryName(outputApksPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(outputApksPath)) File.Delete(outputApksPath);

                using var fs = new FileStream(outputApksPath, FileMode.CreateNew);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

                foreach (var apk in apks)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = zip.CreateEntry(Path.GetFileName(apk), CompressionLevel.Optimal);
                    using var es = entry.Open();
                    using var input = File.OpenRead(apk);
                    input.CopyTo(es);
                }

                return (true, outputApksPath, "");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  UNPACK - .apks file → folder of APKs
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool ok, string output, int count, string error)> UnpackAsync(
        string apksPath, string outputFolder, CancellationToken ct = default)
    {
        if (!File.Exists(apksPath))
            return (false, "", 0, "Source .apks not found");

        return await Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(outputFolder);
                int count = 0;

                using var apksZip = ZipFile.OpenRead(apksPath);
                foreach (var entry in apksZip.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!entry.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) continue;

                    var dest = Path.Combine(outputFolder, entry.Name);
                    if (File.Exists(dest)) File.Delete(dest);
                    entry.ExtractToFile(dest);
                    count++;
                }

                return (true, outputFolder, count, "");
            }
            catch (Exception ex)
            {
                return (false, "", 0, ex.Message);
            }
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  INSTALL - split set → device via install-multiple
    // ═══════════════════════════════════════════════════════════
    public async Task<(bool ok, string message)> InstallToDeviceAsync(
        string serial, SplitApkSet set,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (set == null || set.Splits.Count == 0)
            return (false, "No splits to install");

        // ⭐ Initialize BEFORE try so compiler knows it's always assigned
        string folderForInstall = "";
        bool tempCreated = false;

        try
        {
            if (set.IsApksFile)
            {
                progress?.Report("Extracting .apks to temp...");
                var tempDir = Path.Combine(Path.GetTempPath(),
                    "DeZ4p_SplitInst_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                var (ok, _, _, err) = await UnpackAsync(set.SourcePath, tempDir, ct);
                if (!ok)
                    return (false, $"Extract failed: {err}");

                folderForInstall = tempDir;
                tempCreated = true;
            }
            else
            {
                folderForInstall = set.EffectiveFolder;
            }

            if (string.IsNullOrEmpty(folderForInstall) || !Directory.Exists(folderForInstall))
                return (false, "Install folder is not available");

            var apkPaths = Directory.GetFiles(folderForInstall, "*.apk", SearchOption.TopDirectoryOnly)
                .OrderBy(f => Path.GetFileName(f) == "base.apk" ? 0 : 1)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (apkPaths.Count == 0)
                return (false, "No APKs found for install");

            progress?.Report($"Installing {apkPaths.Count} split(s)...");

            var result = await _adb.InstallSplitApkAsync(serial, apkPaths, progress, ct);

            return result.Success
                ? (true, result.Output)
                : (false, result.Output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            if (tempCreated && !string.IsNullOrEmpty(folderForInstall))
            {
                try { Directory.Delete(folderForInstall, true); } catch { }
            }
        }
    }
}
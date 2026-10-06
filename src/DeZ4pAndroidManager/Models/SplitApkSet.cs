// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Collections.Generic;
using System.Linq;

namespace DeZ4pAndroidManager.Models;

public class SplitApkSet
{
    public string SourcePath { get; set; } = "";       // .apks file OR folder
    public string EffectiveFolder { get; set; } = "";  // real folder with .apk files
    public bool IsApksFile { get; set; }
    public bool IsTemporaryFolder { get; set; }
    public string InferredName { get; set; } = "";     // folder/file base name

    public List<SplitFileInfo> Splits { get; set; } = new();

    public bool HasBase { get; set; }
    public bool IsValidSplitSet => HasBase;

    public long TotalBytes => Splits.Sum(s => s.SizeBytes);
    public string TotalSizeDisplay => MediaItem.FormatSize(TotalBytes);

    public string SplitCountDisplay => $"{Splits.Count} split(s)";

    public List<string> AllArchitectures =>
        Splits.SelectMany(s => s.Architectures).Distinct().OrderBy(x => x).ToList();

    public List<string> AllLanguages =>
        Splits.SelectMany(s => s.Languages).Distinct().OrderBy(x => x).ToList();

    public List<string> AllDensities =>
        Splits.SelectMany(s => s.Densities).Distinct().OrderBy(x => x).ToList();

    public string ArchSummary => AllArchitectures.Count == 0 ? "-" : string.Join(", ", AllArchitectures);
    public string LangSummary => AllLanguages.Count == 0 ? "-" : string.Join(", ", AllLanguages);
    public string DensitySummary => AllDensities.Count == 0 ? "-" : string.Join(", ", AllDensities);

    public string[] AllApkFiles => Splits.Select(s => s.FullPath).ToArray();
}
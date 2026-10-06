// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class StorageAnalyzerViewModel : INotifyPropertyChanged
{
    private readonly StorageAnalyzerService _svc;
    private readonly DeviceStateService _deviceState;

    private DeviceModel? _selectedDevice;
    private bool _isLoading;
    private string _statusMessage = "Ready.";
    private long _totalBytes, _usedBytes, _freeBytes;
    private double _usedPercent;

    public StorageAnalyzerViewModel(StorageAnalyzerService svc, DeviceStateService deviceState,
                                     DeviceWatcherService watcher)
    {
        _svc = svc;
        _deviceState = deviceState;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SaveReportCommand = new RelayCommand(_ => SaveReport());
        OpenReportFolderCommand = new RelayCommand(_ => AppPaths.OpenFolder(AppPaths.Root));

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());

        _ = RefreshAsync();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<StorageCategoryItem> Folders { get; } = new();
    public ObservableCollection<StorageCategoryItem> Categories { get; } = new();
    public ObservableCollection<LargestFile> LargestFiles { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDevice)); }
    }

    public bool HasDevice => _selectedDevice != null;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }

    public long TotalBytes { get => _totalBytes; set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalDisplay)); } }
    public long UsedBytes { get => _usedBytes; set { _usedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsedDisplay)); } }
    public long FreeBytes { get => _freeBytes; set { _freeBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FreeDisplay)); } }
    public double UsedPercent { get => _usedPercent; set { _usedPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsedPercentDisplay)); } }

    public string TotalDisplay => MediaItem.FormatSize(TotalBytes);
    public string UsedDisplay => MediaItem.FormatSize(UsedBytes);
    public string FreeDisplay => MediaItem.FormatSize(FreeBytes);
    public string UsedPercentDisplay => $"{UsedPercent:0.0}%";

    public ICommand RefreshCommand { get; }
    public ICommand SaveReportCommand { get; }
    public ICommand OpenReportFolderCommand { get; }

    private async Task RefreshAsync()
    {
        var cur = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var w = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (w != null)
                foreach (var d in w.CurrentDevices.Where(x => x.IsReady && !x.IsFastboot))
                    Devices.Add(d);
        }
        catch { }

        if (!string.IsNullOrEmpty(cur)) SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];

        if (SelectedDevice == null) { StatusMessage = "No device."; return; }

        IsLoading = true;
        try
        {
            StatusMessage = "Scanning...";
            var (t, u, f) = await _svc.GetTotalAsync(SelectedDevice.Serial);
            TotalBytes = t; UsedBytes = u; FreeBytes = f;
            UsedPercent = t > 0 ? (double)u / t * 100.0 : 0;

            var folders = await _svc.GetFolderSizesAsync(SelectedDevice.Serial);
            long folderTotal = folders.Sum(x => x.SizeBytes);
            if (folderTotal > 0)
                foreach (var it in folders)
                    it.PercentOfTotal = it.SizeBytes * 100.0 / folderTotal;

            Folders.Clear();
            foreach (var x in folders) Folders.Add(x);

            var cats = await _svc.GetCategorySizesAsync(SelectedDevice.Serial);
            long catTotal = cats.Sum(x => x.SizeBytes);
            if (catTotal > 0)
                foreach (var it in cats)
                    it.PercentOfTotal = it.SizeBytes * 100.0 / catTotal;

            Categories.Clear();
            foreach (var x in cats) Categories.Add(x);

            var largest = await _svc.GetLargestFilesAsync(SelectedDevice.Serial);
            LargestFiles.Clear();
            foreach (var x in largest) LargestFiles.Add(x);

            StatusMessage = $"Scanned: {folders.Count} folders, {largest.Count} largest files";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private void SaveReport()
    {
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"storage_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            var sb = new StringBuilder();
            sb.AppendLine($"DeZ4p Storage Report");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine();
            sb.AppendLine($"Total: {TotalDisplay}");
            sb.AppendLine($"Used:  {UsedDisplay} ({UsedPercentDisplay})");
            sb.AppendLine($"Free:  {FreeDisplay}");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ Top folders â”€â”€");
            foreach (var x in Folders) sb.AppendLine($"  {x.SizeDisplay,10}  {x.PercentDisplay,6}  {x.Name}");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ Categories â”€â”€");
            foreach (var x in Categories) sb.AppendLine($"  {x.SizeDisplay,10}  {x.PercentDisplay,6}  {x.Name} ({x.CountDisplay})");
            sb.AppendLine();
            sb.AppendLine("â”€â”€ Largest files â”€â”€");
            foreach (var x in LargestFiles) sb.AppendLine($"  {x.SizeDisplay,10}  {x.Path}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"âœ“ Saved: Reports\\{Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"âœ— {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
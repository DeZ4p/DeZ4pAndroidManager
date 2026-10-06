// © DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class PartitionToolsViewModel : INotifyPropertyChanged
{
    private readonly PartitionService _svc;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private DeviceModel? _selectedDevice;
    private PartitionInfo? _selected;
    private bool _isLoading;
    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private string _searchText = "";

    public PartitionToolsViewModel(PartitionService svc, DeviceStateService deviceState,
                                    DeviceWatcherService watcher, ActivityLogService activity)
    {
        _svc = svc;
        _deviceState = deviceState;
        _activity = activity;

        FilteredPartitions = CollectionViewSource.GetDefaultView(Partitions);
        FilteredPartitions.Filter = FilterPartition;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        BackupCommand = new AsyncRelayCommand(BackupAsync);
        ExportListCommand = new RelayCommand(_ => ExportList());
        CopyNameCommand = new RelayCommand(_ => CopyName());

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(async () => await RefreshAsync());
        _ = RefreshAsync();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<PartitionInfo> Partitions { get; } = new();
    public ICollectionView FilteredPartitions { get; }

    public DeviceModel? SelectedDevice { get => _selectedDevice; set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFastboot)); } }
    public PartitionInfo? Selected { get => _selected; set { _selected = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelected)); } }
    public bool HasSelected => _selected != null;
    public bool IsFastboot => _selectedDevice?.IsFastboot == true;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }
    public string SearchText { get => _searchText; set { _searchText = value ?? ""; OnPropertyChanged(); FilteredPartitions.Refresh(); UpdateCount(); } }
    public int PartitionCount => Partitions.Count;
    public int FilteredCount => FilteredPartitions.Cast<object>().Count();

    public ICommand RefreshCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand ExportListCommand { get; }
    public ICommand CopyNameCommand { get; }

    private bool FilterPartition(object o)
    {
        if (o is not PartitionInfo p) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var q = SearchText.Trim();
        return p.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
            || p.Type.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void UpdateCount()
    {
        OnPropertyChanged(nameof(PartitionCount));
        OnPropertyChanged(nameof(FilteredCount));
        StatusMessage = $"{FilteredCount} of {PartitionCount} partition(s)";
    }

    private async Task RefreshAsync()
    {
        var cur = _selectedDevice?.Serial;
        Devices.Clear();
        try
        {
            var w = App.Services.GetService(typeof(DeviceWatcherService)) as DeviceWatcherService;
            if (w != null) foreach (var d in w.CurrentDevices.Where(x => x.IsFastboot)) Devices.Add(d);
        }
        catch { }
        if (!string.IsNullOrEmpty(cur)) SelectedDevice = Devices.FirstOrDefault(d => d.Serial == cur);
        if (SelectedDevice == null && Devices.Count > 0) SelectedDevice = Devices[0];

        Partitions.Clear();
        OnPropertyChanged(nameof(PartitionCount));
        OnPropertyChanged(nameof(FilteredCount));

        if (SelectedDevice == null) { StatusMessage = "No fastboot device."; return; }
        IsLoading = true;
        try
        {
            StatusMessage = "Reading partitions...";
            var list = await _svc.GetPartitionsAsync(SelectedDevice.Serial);
            foreach (var p in list) Partitions.Add(p);
            FilteredPartitions.Refresh();
            UpdateCount();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private async Task BackupAsync()
    {
        if (SelectedDevice == null || Selected == null) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = $"Backup {Selected.Name}",
            FileName = $"{Selected.Name}_{DateTime.Now:yyyy-MM-dd_HH-mm}.img",
            Filter = "Image files (*.img)|*.img|All files (*.*)|*.*"
        };
        if (sfd.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            StatusMessage = $"Backing up {Selected.Name}...";
            var (ok, msg) = await _svc.BackupPartitionAsync(SelectedDevice.Serial, Selected.Name, sfd.FileName);
            StatusMessage = ok ? $"✓ Saved: {Path.GetFileName(sfd.FileName)}" : $"✗ {msg}";
            if (ok) _activity.LogBackup($"Partition backup: {Selected.Name}");
        }
        finally { IsBusy = false; }
    }

    private void ExportList()
    {
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"partitions_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Partition List");
            sb.AppendLine($"Device: {SelectedDevice?.DisplayName}");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total: {PartitionCount} partition(s)");
            sb.AppendLine();
            sb.AppendLine($"{"Name",-32} {"Size",15} {"Type",-20}");
            sb.AppendLine(new string('-', 70));
            foreach (var p in Partitions)
                sb.AppendLine($"{p.Name,-32} {p.Size,15} {p.Type,-20}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"✓ Saved: {Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"✗ {ex.Message}"; }
    }

    private void CopyName()
    {
        if (Selected == null) return;
        try { Clipboard.SetText(Selected.Name); StatusMessage = $"Copied: {Selected.Name}"; } catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
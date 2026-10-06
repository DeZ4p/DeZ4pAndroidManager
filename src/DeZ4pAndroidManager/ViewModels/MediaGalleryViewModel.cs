// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class MediaGalleryViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly MediaGalleryService _media;
    private readonly DeviceStateService _deviceState;

    private string _currentSerial = string.Empty;
    private string _searchText = "";
    private string _filterKind = "all";
    private bool _isLoading;
    private bool _isBusy;
    private bool _isPreviewLoading;
    private string _statusMessage = "";
    private MediaItem? _selectedItem;

    // Preview state
    private BitmapImage? _previewImage;
    private bool _hasImagePreview;
    private bool _hasVideoPreview;
    private bool _hasAudioPreview;
    private bool _hasOtherPreview;
    private string _previewPath = "";
    private CancellationTokenSource? _previewCts;

    public MediaGalleryViewModel(AdbService adb, MediaGalleryService media, DeviceStateService deviceState)
    {
        _adb = adb;
        _media = media;
        _deviceState = deviceState;

        FilteredItems = CollectionViewSource.GetDefaultView(Items);
        FilteredItems.Filter = FilterItem;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SetFilterCommand = new AsyncRelayCommand<string>(SetFilterAsync);
        DownloadCommand = new AsyncRelayCommand(DownloadSelectedAsync);
        DownloadAllCommand = new AsyncRelayCommand(DownloadAllAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        PlayCommand = new AsyncRelayCommand(PlaySelectedAsync);
        OpenWithCommand = new AsyncRelayCommand(OpenWithPickerAsync);
        CopyPathCommand = new RelayCommand(_ => CopyPath());

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try { await OnDeviceChangedAsync(); } catch { }
            });
        };

        _ = OnDeviceChangedAsync();
    }

    public ObservableCollection<MediaItem> Items { get; } = new();
    public ObservableCollection<MediaItem> SelectedItems { get; } = new();
    public ICollectionView FilteredItems { get; }

    public MediaItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(PrimaryActionIcon));
            OnPropertyChanged(nameof(PrimaryActionColor));
            _ = LoadPreviewAsync();
        }
    }

    public bool HasSelectedItem => _selectedItem != null;

    /// <summary>Label for the primary action button (Open for images, Play for video/audio).</summary>
    public string PrimaryActionLabel
    {
        get
        {
            if (_selectedItem == null) return LocalizationService.Translate("Media.Open");
            return _selectedItem.Kind switch
            {
                MediaKind.Image => LocalizationService.Translate("Media.Open"),
                MediaKind.Video => LocalizationService.Translate("Media.Play"),
                MediaKind.Audio => LocalizationService.Translate("Media.Play"),
                _ => LocalizationService.Translate("Media.Open")
            };
        }
    }

    /// <summary>Icon glyph for the primary action button.</summary>
    public string PrimaryActionIcon
    {
        get
        {
            if (_selectedItem == null) return "\uE8E5"; // Open icon
            return _selectedItem.Kind switch
            {
                MediaKind.Image => "\uE8E5", // Open icon
                MediaKind.Video => "\uE768", // Play icon
                MediaKind.Audio => "\uE768", // Play icon
                _ => "\uE8E5"
            };
        }
    }

    /// <summary>Color for the primary action button.</summary>
    public string PrimaryActionColor
    {
        get
        {
            if (_selectedItem == null) return "#4F8CFF";
            return _selectedItem.Kind switch
            {
                MediaKind.Image => "#4F8CFF", // Blue for Open
                MediaKind.Video => "#34D399", // Green for Play
                MediaKind.Audio => "#22D3EE", // Cyan for Play
                _ => "#4F8CFF"
            };
        }
    }

    /// <summary>Label for the bottom action bar button (same logic).</summary>
    public string BottomPrimaryLabel
    {
        get
        {
            if (SelectedItems.Count == 0) return LocalizationService.Translate("Media.Open");
            var first = SelectedItems[0];
            return first.Kind switch
            {
                MediaKind.Image => LocalizationService.Translate("Media.Open"),
                MediaKind.Video => LocalizationService.Translate("Media.Play"),
                MediaKind.Audio => LocalizationService.Translate("Media.Play"),
                _ => LocalizationService.Translate("Media.Open")
            };
        }
    }

    public string BottomPrimaryIcon
    {
        get
        {
            if (SelectedItems.Count == 0) return "\uE8E5";
            var first = SelectedItems[0];
            return first.Kind switch
            {
                MediaKind.Image => "\uE8E5",
                MediaKind.Video => "\uE768",
                MediaKind.Audio => "\uE768",
                _ => "\uE8E5"
            };
        }
    }

    public string BottomPrimaryColor
    {
        get
        {
            if (SelectedItems.Count == 0) return "#4F8CFF";
            var first = SelectedItems[0];
            return first.Kind switch
            {
                MediaKind.Image => "#4F8CFF",
                MediaKind.Video => "#34D399",
                MediaKind.Audio => "#22D3EE",
                _ => "#4F8CFF"
            };
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; OnPropertyChanged(); FilteredItems.Refresh(); UpdateStatus(); }
    }

    public string FilterKind
    {
        get => _filterKind;
        set
        {
            _filterKind = value ?? "all";
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(IsImageSelected));
            OnPropertyChanged(nameof(IsVideoSelected));
            OnPropertyChanged(nameof(IsAudioSelected));
        }
    }

    public bool IsAllSelected => FilterKind == "all";
    public bool IsImageSelected => FilterKind == "image";
    public bool IsVideoSelected => FilterKind == "video";
    public bool IsAudioSelected => FilterKind == "audio";

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        set { _isPreviewLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public bool HasDevice => !string.IsNullOrEmpty(_currentSerial);
    public bool HasSelection => SelectedItems.Count > 0;
    public int SelectionCount => SelectedItems.Count;

    // ═══ PREVIEW STATE ═══
    public BitmapImage? PreviewImage
    {
        get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(); }
    }

    public bool HasImagePreview
    {
        get => _hasImagePreview;
        set { _hasImagePreview = value; OnPropertyChanged(); }
    }

    public bool HasVideoPreview
    {
        get => _hasVideoPreview;
        set { _hasVideoPreview = value; OnPropertyChanged(); }
    }

    public bool HasAudioPreview
    {
        get => _hasAudioPreview;
        set { _hasAudioPreview = value; OnPropertyChanged(); }
    }

    public bool HasOtherPreview
    {
        get => _hasOtherPreview;
        set { _hasOtherPreview = value; OnPropertyChanged(); }
    }

    public string PreviewPath
    {
        get => _previewPath;
        set { _previewPath = value ?? ""; OnPropertyChanged(); }
    }

    public string PreviewName => _selectedItem?.DisplayName ?? "";
    public string PreviewKind => _selectedItem?.KindDisplay ?? "";
    public string PreviewSize => _selectedItem?.SizeDisplay ?? "";
    public string PreviewDimensions => _selectedItem?.DimensionsDisplay ?? "";
    public string PreviewDate => _selectedItem?.DateDisplay ?? "";
    public string PreviewIcon => _selectedItem?.IconGlyph ?? "\uE7C3";

    public ICommand RefreshCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand DownloadAllCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand OpenWithCommand { get; }
    public ICommand CopyPathCommand { get; }

    public void UpdateSelection(IList<MediaItem> selected)
    {
        SelectedItems.Clear();
        foreach (var s in selected) SelectedItems.Add(s);
        SelectedItem = SelectedItems.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(BottomPrimaryLabel));
        OnPropertyChanged(nameof(BottomPrimaryIcon));
        OnPropertyChanged(nameof(BottomPrimaryColor));
        if (SelectedItems.Count > 0)
            StatusMessage = $"{SelectedItems.Count} {LocalizationService.Translate("Media.ItemsSelected")}";
        else
            UpdateStatus();
    }

    private bool FilterItem(object o)
    {
        if (o is not MediaItem m) return false;
        switch (FilterKind)
        {
            case "image": if (m.Kind != MediaKind.Image) return false; break;
            case "video": if (m.Kind != MediaKind.Video) return false; break;
            case "audio": if (m.Kind != MediaKind.Audio) return false; break;
        }
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var q = SearchText.Trim();
        return (m.DisplayName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (m.FilePath ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatus()
    {
        var count = FilteredItems.Cast<object>().Count();
        StatusMessage = count == 0
            ? LocalizationService.Translate("Media.Empty")
            : $"{count} {LocalizationService.Translate("Media.ItemsCount")}";
    }

    private async Task OnDeviceChangedAsync()
    {
        var d = _deviceState.CurrentDevice;
        if (d == null || d.IsFastboot)
        {
            _currentSerial = "";
            Items.Clear();
            SelectedItems.Clear();
            ClearPreview();
            StatusMessage = d == null
                ? LocalizationService.Translate("Media.NoDevice")
                : LocalizationService.Translate("Media.NeedAdb");
            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(HasSelection));
            return;
        }

        _currentSerial = d.Serial;
        OnPropertyChanged(nameof(HasDevice));
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (!HasDevice) return;
        IsLoading = true;
        try
        {
            var list = await _media.ListAllAsync(_currentSerial);
            Items.Clear();
            foreach (var m in list) Items.Add(m);
            FilteredItems.Refresh();
            SelectedItems.Clear();
            SelectedItem = null;
            ClearPreview();
            UpdateStatus();
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    private Task SetFilterAsync(string? kind)
    {
        FilterKind = string.IsNullOrEmpty(kind) ? "all" : kind;
        FilteredItems.Refresh();
        UpdateStatus();
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════
    //  PREVIEW
    // ═══════════════════════════════════════════════════════════
    private void ClearPreview()
    {
        PreviewImage = null;
        HasImagePreview = false;
        HasVideoPreview = false;
        HasAudioPreview = false;
        HasOtherPreview = false;
        PreviewPath = "";
        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewKind));
        OnPropertyChanged(nameof(PreviewSize));
        OnPropertyChanged(nameof(PreviewDimensions));
        OnPropertyChanged(nameof(PreviewDate));
        OnPropertyChanged(nameof(PreviewIcon));
    }

    private async Task LoadPreviewAsync()
    {
        ClearPreview();
        if (_selectedItem == null || !HasDevice) return;

        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewKind));
        OnPropertyChanged(nameof(PreviewSize));
        OnPropertyChanged(nameof(PreviewDimensions));
        OnPropertyChanged(nameof(PreviewDate));
        OnPropertyChanged(nameof(PreviewIcon));

        var item = _selectedItem;

        // Only auto-load image previews (fast).
        // Video / Audio just show info + Play button - user clicks to open.
        if (item.Kind == MediaKind.Image)
        {
            IsPreviewLoading = true;
            HasImagePreview = true;
            try
            {
                _previewCts?.Cancel();
                _previewCts = new CancellationTokenSource();
                var token = _previewCts.Token;

                var local = await _media.PullToCacheAsync(_currentSerial, item, token);
                if (local == null || token.IsCancellationRequested) return;

                PreviewPath = local;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(local, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                PreviewImage = bmp;
            }
            catch { }
            finally { IsPreviewLoading = false; }
        }
        else if (item.Kind == MediaKind.Video)
        {
            HasVideoPreview = true;
        }
        else if (item.Kind == MediaKind.Audio)
        {
            HasAudioPreview = true;
        }
        else
        {
            HasOtherPreview = true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ACTIONS
    // ═══════════════════════════════════════════════════════════
    private async Task PlaySelectedAsync()
    {
        if (SelectedItem == null) return;
        IsBusy = true;
        try
        {
            StatusMessage = $"▶ {LocalizationService.Translate("Media.Opening")}";
            var ok = await _media.OpenWithSystemAsync(_currentSerial, SelectedItem);
            StatusMessage = ok
                ? $"✅ {LocalizationService.Translate("Media.OpenedWithDefault")}"
                : $"❌ {LocalizationService.Translate("Media.DownloadFailed")}";
        }
        finally { IsBusy = false; }
    }

    private async Task OpenWithPickerAsync()
    {
        if (SelectedItem == null) return;
        IsBusy = true;
        try
        {
            StatusMessage = $"📂 {LocalizationService.Translate("Media.Opening")}";
            var ok = await _media.OpenWithPickerAsync(_currentSerial, SelectedItem);
            StatusMessage = ok
                ? $"✅ {LocalizationService.Translate("Media.PickerOpened")}"
                : $"❌ {LocalizationService.Translate("Media.DownloadFailed")}";
        }
        finally { IsBusy = false; }
    }

    private async Task DownloadSelectedAsync()
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = LocalizationService.Translate("Media.SelectFirst");
            return;
        }

        var ofd = new OpenFolderDialog { Title = LocalizationService.Translate("Media.ChooseDestFolder") };
        if (ofd.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in SelectedItems.ToList())
            {
                StatusMessage = $"↓ {item.DisplayName}";
                var name = MediaGalleryService.SanitizeFileName(item.DisplayName);
                var dest = Path.Combine(ofd.FolderName, name);
                if (await _media.PullAsync(_currentSerial, item.FilePath, dest)) ok++;
            }
            StatusMessage = $"✅ {LocalizationService.Translate("Media.Downloaded")} {ok}/{SelectedItems.Count}";
        }
        finally { IsBusy = false; }
    }

    private async Task DownloadAllAsync()
    {
        if (!HasDevice) return;
        var ofd = new OpenFolderDialog { Title = LocalizationService.Translate("Media.ChooseDestFolder") };
        if (ofd.ShowDialog() != true) return;

        var all = FilteredItems.Cast<MediaItem>().ToList();
        if (all.Count == 0) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in all)
            {
                StatusMessage = $"↓ {item.DisplayName} ({ok + 1}/{all.Count})";
                var name = MediaGalleryService.SanitizeFileName(item.DisplayName);
                var dest = Path.Combine(ofd.FolderName, name);
                if (await _media.PullAsync(_currentSerial, item.FilePath, dest)) ok++;
            }
            StatusMessage = $"✅ {LocalizationService.Translate("Media.Downloaded")} {ok}/{all.Count}";
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedItems.Count == 0) return;

        var msg = SelectedItems.Count == 1
            ? string.Format(LocalizationService.Translate("Media.ConfirmDeleteMsg"), SelectedItems[0].DisplayName)
            : string.Format(LocalizationService.Translate("Media.ConfirmDeleteManyMsg"), SelectedItems.Count);

        var result = MessageBox.Show(msg,
            LocalizationService.Translate("Media.ConfirmDeleteTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var item in SelectedItems.ToList())
                if (await _media.DeleteAsync(_currentSerial, item.FilePath)) ok++;

            StatusMessage = $"✅ {LocalizationService.Translate("Media.Deleted")} {ok}/{SelectedItems.Count}";
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    private void CopyPath()
    {
        if (SelectedItems.Count == 0) return;
        try
        {
            System.Windows.Clipboard.SetText(
                string.Join(Environment.NewLine, SelectedItems.Select(i => i.FilePath)));
            StatusMessage = $"📋 {LocalizationService.Translate("Media.PathCopied")}";
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
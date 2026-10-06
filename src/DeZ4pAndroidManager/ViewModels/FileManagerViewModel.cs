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
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.Views;
using Microsoft.Win32;

namespace DeZ4pAndroidManager.ViewModels;

public class FileManagerViewModel : INotifyPropertyChanged
{
    private readonly AdbService _adb;
    private readonly FileManagerService _files;
    private readonly DeviceStateService _deviceState;

    private string _currentSerial = string.Empty;
    private string _currentPath = "";
    private FileItem? _selectedItem;
    private bool _isBusy;
    private bool _isLoading;
    private string _statusMessage = "";

    private readonly List<string> _clipboardPaths = new();
    private bool _clipboardIsCut = false;

    // Preview state
    private BitmapImage? _previewImage;
    private bool _hasImagePreview;
    private bool _isPreviewLoading;
    private CancellationTokenSource? _previewCts;

    public FileManagerViewModel(AdbService adb, FileManagerService files, DeviceStateService deviceState)
    {
        _adb = adb;
        _files = files;
        _deviceState = deviceState;

        NavigateToCommand = new AsyncRelayCommand<string>(NavigateToAsync);
        NavigateUpCommand = new AsyncRelayCommand(NavigateUpAsync);
        HomeCommand = new AsyncRelayCommand(HomeAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenItemCommand = new AsyncRelayCommand(OpenSelectedAsync);
        OpenWithCommand = new AsyncRelayCommand(OpenWithPickerAsync);
        DownloadCommand = new AsyncRelayCommand(DownloadSelectedAsync);
        UploadCommand = new AsyncRelayCommand(UploadAsync);
        NewFolderCommand = new AsyncRelayCommand(NewFolderAsync);
        RenameCommand = new AsyncRelayCommand(RenameSelectedAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        CopyCommand = new RelayCommand(_ => CopySelected(cut: false));
        CutCommand = new RelayCommand(_ => CopySelected(cut: true));
        PasteCommand = new AsyncRelayCommand(PasteAsync);
        CopyPathCommand = new RelayCommand(_ => CopyPathToClipboard());

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try { await OnDeviceChangedAsync(); } catch { }
            });
        };

        _ = OnDeviceChangedAsync();
    }

    public ObservableCollection<FileItem> Items { get; } = new();
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();
    public ObservableCollection<FileItem> SelectedItems { get; } = new();

    public FileItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasSelectedFile));
            OnPropertyChanged(nameof(PreviewName));
            OnPropertyChanged(nameof(PreviewKind));
            OnPropertyChanged(nameof(PreviewSize));
            OnPropertyChanged(nameof(PreviewModified));
            OnPropertyChanged(nameof(PreviewFullPath));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(PrimaryActionIcon));
            OnPropertyChanged(nameof(PrimaryActionColor));
            _ = LoadPreviewAsync();
        }
    }

    public string CurrentPath
    {
        get => _currentPath;
        set { _currentPath = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public bool HasDevice => !string.IsNullOrEmpty(_currentSerial);
    public bool HasSelection => SelectedItems.Count > 0;
    public bool HasClipboard => _clipboardPaths.Count > 0;
    public bool CanNavigateUp => CurrentPath != "/" && !string.IsNullOrEmpty(CurrentPath);
    public int SelectionCount => SelectedItems.Count;

    // ═══ PREVIEW ═══
    public bool HasSelectedFile => _selectedItem != null && !_selectedItem.IsDirectory;

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

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        set { _isPreviewLoading = value; OnPropertyChanged(); }
    }

    public string PreviewName => _selectedItem?.Name ?? "";
    public string PreviewKind => _selectedItem?.TypeDisplay ?? "";
    public string PreviewSize => _selectedItem?.SizeDisplay ?? "";
    public string PreviewModified => _selectedItem?.ModifiedDisplay ?? "";
    public string PreviewFullPath => _selectedItem?.FullPath ?? "";

    public string PrimaryActionLabel
    {
        get
        {
            if (_selectedItem == null) return LocalizationService.Translate("FileManager.Open");
            if (_selectedItem.IsDirectory) return LocalizationService.Translate("FileManager.Open");
            return GetKind(_selectedItem.Name) switch
            {
                FileKind.Image => LocalizationService.Translate("FileManager.Open"),
                FileKind.Video => LocalizationService.Translate("FileManager.Play"),
                FileKind.Audio => LocalizationService.Translate("FileManager.Play"),
                _ => LocalizationService.Translate("FileManager.Open")
            };
        }
    }

    public string PrimaryActionIcon
    {
        get
        {
            if (_selectedItem == null) return "\uE8E5";
            if (_selectedItem.IsDirectory) return "\uE8B7";
            return GetKind(_selectedItem.Name) switch
            {
                FileKind.Image => "\uE8E5",
                FileKind.Video => "\uE768",
                FileKind.Audio => "\uE768",
                _ => "\uE8E5"
            };
        }
    }

    public string PrimaryActionColor
    {
        get
        {
            if (_selectedItem == null) return "#4F8CFF";
            if (_selectedItem.IsDirectory) return "#F59E0B";
            return GetKind(_selectedItem.Name) switch
            {
                FileKind.Image => "#4F8CFF",
                FileKind.Video => "#34D399",
                FileKind.Audio => "#22D3EE",
                _ => "#4F8CFF"
            };
        }
    }

    private enum FileKind { Image, Video, Audio, Other }

    private static FileKind GetKind(string name)
    {
        var ext = Path.GetExtension(name ?? "").ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => FileKind.Image,
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".3gp" or ".m4v" or ".ts" => FileKind.Video,
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" or ".opus" or ".wma" => FileKind.Audio,
            _ => FileKind.Other
        };
    }

    private static bool IsOpenable(string name)
    {
        var ext = Path.GetExtension(name ?? "").ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico"
                or ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".3gp" or ".m4v" or ".ts"
                or ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" or ".aac" or ".opus" or ".wma"
                or ".pdf" or ".txt" or ".md" or ".log" or ".json" or ".xml" or ".html" or ".htm"
                or ".csv" or ".ini" or ".conf" or ".yaml" or ".yml" => true,
            _ => false
        };
    }

    public ICommand NavigateToCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand HomeCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenItemCommand { get; }
    public ICommand OpenWithCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand NewFolderCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand CopyPathCommand { get; }

    // ═══════════════════════════════════════════════════════════
    //  SELECTION
    // ═══════════════════════════════════════════════════════════
    public void UpdateSelection(IList<FileItem> items)
    {
        SelectedItems.Clear();
        foreach (var item in items) SelectedItems.Add(item);
        SelectedItem = SelectedItems.FirstOrDefault();

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCount));

        StatusMessage = SelectedItems.Count == 0
            ? LocalizationService.Translate("FileManager.NothingSelected")
            : $"{SelectedItems.Count} {LocalizationService.Translate("FileManager.ItemsSelected")}";
    }

    // ═══════════════════════════════════════════════════════════
    //  PREVIEW
    // ═══════════════════════════════════════════════════════════
    private void ClearPreview()
    {
        PreviewImage = null;
        HasImagePreview = false;
        IsPreviewLoading = false;
    }

    private async Task LoadPreviewAsync()
    {
        ClearPreview();
        if (_selectedItem == null || _selectedItem.IsDirectory || !HasDevice) return;

        var item = _selectedItem;
        var kind = GetKind(item.Name);

        // Only auto-preview images
        if (kind == FileKind.Image)
        {
            IsPreviewLoading = true;
            HasImagePreview = true;
            try
            {
                _previewCts?.Cancel();
                _previewCts = new CancellationTokenSource();
                var token = _previewCts.Token;

                var local = await PullToCacheAsync(item, token);
                if (local == null || token.IsCancellationRequested) return;

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
    }

    private async Task<string?> PullToCacheAsync(FileItem item, CancellationToken ct)
    {
        MediaGalleryService.EnsureCacheDir();
        var safeName = MediaGalleryService.SanitizeFileName(item.Name);
        var hash = Math.Abs(item.FullPath.GetHashCode()).ToString("X8");
        var cached = Path.Combine(MediaGalleryService.CacheDir, $"{hash}_{safeName}");

        if (File.Exists(cached) && item.SizeBytes > 0 && new FileInfo(cached).Length == item.SizeBytes)
            return cached;

        try { if (File.Exists(cached)) File.Delete(cached); } catch { }

        var r = await _adb.ExecuteRawAsync(
            $"-s {_currentSerial} pull \"{item.FullPath}\" \"{cached}\"", 600000, ct);
        return r.ExitCode == 0 && File.Exists(cached) ? cached : null;
    }

    // ═══════════════════════════════════════════════════════════
    //  DEVICE STATE
    // ═══════════════════════════════════════════════════════════
    private async Task OnDeviceChangedAsync()
    {
        var device = _deviceState.CurrentDevice;

        if (device == null || device.IsFastboot)
        {
            _currentSerial = string.Empty;
            Items.Clear();
            Breadcrumbs.Clear();
            SelectedItems.Clear();
            CurrentPath = "";
            ClearPreview();
            StatusMessage = device == null
                ? LocalizationService.Translate("FileManager.NoDevice")
                : LocalizationService.Translate("FileManager.NeedAdb");
            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanNavigateUp));
            return;
        }

        _currentSerial = device.Serial;
        OnPropertyChanged(nameof(HasDevice));

        if (string.IsNullOrEmpty(CurrentPath))
            await HomeAsync();
    }

    // ═══════════════════════════════════════════════════════════
    //  NAVIGATION
    // ═══════════════════════════════════════════════════════════
    private async Task HomeAsync()
    {
        if (!HasDevice) return;
        var home = await _files.GetHomePathAsync(_currentSerial);
        await NavigateToAsync(home);
    }

    private async Task NavigateUpAsync()
    {
        if (!HasDevice || !CanNavigateUp) return;
        var parent = FileManagerService.GetParentPath(CurrentPath);
        await NavigateToAsync(parent);
    }

    private async Task NavigateToAsync(string? path)
    {
        if (!HasDevice || string.IsNullOrEmpty(path)) return;

        IsLoading = true;
        try
        {
            var items = await _files.ListDirectoryAsync(_currentSerial, path);
            CurrentPath = path;
            Items.Clear();
            foreach (var item in items) Items.Add(item);

            SelectedItem = null;
            SelectedItems.Clear();
            ClearPreview();
            BuildBreadcrumbs(path);

            StatusMessage = items.Count == 0
                ? LocalizationService.Translate("FileManager.EmptyFolder")
                : $"{items.Count} - {items.Count(i => i.IsDirectory)} {LocalizationService.Translate("FileManager.Folder")} / {items.Count(i => !i.IsDirectory)} {LocalizationService.Translate("FileManager.FileLabel")}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanNavigateUp));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    private void BuildBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        if (string.IsNullOrEmpty(path)) return;

        Breadcrumbs.Add(new BreadcrumbItem { Name = "/", Path = "/" });

        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var accumulated = "";
        foreach (var part in parts)
        {
            accumulated += "/" + part;
            Breadcrumbs.Add(new BreadcrumbItem { Name = part, Path = accumulated });
        }
    }

    public async Task RefreshAsync()
    {
        if (!HasDevice) return;
        if (string.IsNullOrEmpty(CurrentPath)) await HomeAsync();
        else await NavigateToAsync(CurrentPath);
    }

    // ═══════════════════════════════════════════════════════════
    //  OPEN (double-click or Play/Open button)
    // ═══════════════════════════════════════════════════════════
    public async Task OpenSelectedAsync()
    {
        if (SelectedItem == null) return;

        if (SelectedItem.IsDirectory)
        {
            await NavigateToAsync(SelectedItem.FullPath);
            return;
        }

        if (!IsOpenable(SelectedItem.Name))
        {
            StatusMessage = $"{LocalizationService.Translate("FileManager.CannotOpen")}: {SelectedItem.Name}";
            return;
        }

        await OpenWithDefaultAsync(SelectedItem);
    }

    private async Task OpenWithDefaultAsync(FileItem item)
    {
        IsBusy = true;
        try
        {
            StatusMessage = $"↓ {item.Name}...";
            var cached = await PullToCacheAsync(item, CancellationToken.None);
            if (cached == null)
            {
                StatusMessage = $"❌ {LocalizationService.Translate("FileManager.DownloadFailed")}";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cached,
                    UseShellExecute = true
                });
                StatusMessage = $"✅ {LocalizationService.Translate("FileManager.Opened")}: {item.Name}";
            }
            catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        }
        finally { IsBusy = false; }
    }

    private async Task OpenWithPickerAsync()
    {
        if (SelectedItem == null || SelectedItem.IsDirectory) return;
        if (!IsOpenable(SelectedItem.Name))
        {
            StatusMessage = $"{LocalizationService.Translate("FileManager.CannotOpen")}: {SelectedItem.Name}";
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = $"📂 {LocalizationService.Translate("FileManager.Opening")}";
            var cached = await PullToCacheAsync(SelectedItem, CancellationToken.None);
            if (cached == null)
            {
                StatusMessage = $"❌ {LocalizationService.Translate("FileManager.DownloadFailed")}";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL \"{cached}\"",
                    UseShellExecute = true
                });
                StatusMessage = $"✅ {LocalizationService.Translate("FileManager.PickerOpened")}";
            }
            catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        }
        finally { IsBusy = false; }
    }

    // ═══════════════════════════════════════════════════════════
    //  CLIPBOARD
    // ═══════════════════════════════════════════════════════════
    private void CopySelected(bool cut)
    {
        if (SelectedItems.Count == 0)
        {
            StatusMessage = LocalizationService.Translate("FileManager.NothingSelected");
            return;
        }

        _clipboardPaths.Clear();
        _clipboardPaths.AddRange(SelectedItems.Select(i => i.FullPath));
        _clipboardIsCut = cut;
        OnPropertyChanged(nameof(HasClipboard));

        var count = _clipboardPaths.Count;
        StatusMessage = cut
            ? $"✂ {LocalizationService.Translate("FileManager.CutDone")} ({count})"
            : $"📋 {LocalizationService.Translate("FileManager.Copied")} ({count})";
    }

    private async Task PasteAsync()
    {
        if (!HasDevice) { StatusMessage = LocalizationService.Translate("FileManager.NoDevice"); return; }
        if (_clipboardPaths.Count == 0) { StatusMessage = LocalizationService.Translate("FileManager.ClipboardEmpty"); return; }

        IsBusy = true;
        var okCount = 0;
        var failCount = 0;
        var cutDone = new List<string>();

        try
        {
            foreach (var srcPath in _clipboardPaths)
            {
                var name = srcPath.TrimEnd('/').Split('/').Last();
                var destPath = CurrentPath.TrimEnd('/') + "/" + name;

                if (await _files.PathExistsAsync(_currentSerial, destPath))
                {
                    if (_clipboardIsCut && FileManagerService.GetParentPath(srcPath) == CurrentPath.TrimEnd('/'))
                        continue;

                    for (int i = 1; i < 200; i++)
                    {
                        var candidate = CurrentPath.TrimEnd('/') + "/" + name + $" (copy {i})";
                        if (!await _files.PathExistsAsync(_currentSerial, candidate)) { destPath = candidate; break; }
                    }
                }

                bool success = _clipboardIsCut
                    ? await _files.MoveAsync(_currentSerial, srcPath, destPath)
                    : await _files.CopyAsync(_currentSerial, srcPath, destPath);

                if (success) { okCount++; if (_clipboardIsCut) cutDone.Add(srcPath); }
                else failCount++;
            }

            if (_clipboardIsCut && cutDone.Count > 0)
            {
                foreach (var p in cutDone) _clipboardPaths.Remove(p);
                OnPropertyChanged(nameof(HasClipboard));
            }

            StatusMessage = failCount == 0
                ? $"✅ {LocalizationService.Translate("FileManager.Pasted")} ({okCount})"
                : $"⚠ {okCount} OK / {failCount} failed";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void CopyPathToClipboard()
    {
        if (SelectedItems.Count == 0) return;
        var paths = string.Join(Environment.NewLine, SelectedItems.Select(i => i.FullPath));
        try
        {
            System.Windows.Clipboard.SetText(paths);
            StatusMessage = $"📋 {LocalizationService.Translate("FileManager.PathCopied")}";
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════
    //  FILE OPS
    // ═══════════════════════════════════════════════════════════
    private async Task DownloadSelectedAsync()
    {
        if (SelectedItems.Count == 0) { StatusMessage = LocalizationService.Translate("FileManager.SelectToDownload"); return; }
        var single = SelectedItems.FirstOrDefault();

        IsBusy = true;
        try
        {
            if (SelectedItems.Count > 1)
            {
                var ofd = new OpenFolderDialog { Title = "Choose destination folder" };
                if (ofd.ShowDialog() != true) return;
                int ok = 0;
                foreach (var item in SelectedItems)
                {
                    StatusMessage = $"↓ {item.Name}";
                    if (await _files.PullAsync(_currentSerial, item.FullPath, ofd.FolderName)) ok++;
                }
                StatusMessage = $"✅ {ok}/{SelectedItems.Count}";
                return;
            }

            if (single!.IsDirectory)
            {
                var ofd = new OpenFolderDialog { Title = "Choose where to save the folder" };
                if (ofd.ShowDialog() != true) return;
                StatusMessage = LocalizationService.Translate("FileManager.DownloadingFolder");
                var ok = await _files.PullAsync(_currentSerial, single.FullPath, ofd.FolderName);
                StatusMessage = ok
                    ? $"✅ {LocalizationService.Translate("FileManager.Downloaded")}: {single.Name}"
                    : $"❌ {LocalizationService.Translate("FileManager.DownloadFailed")}";
            }
            else
            {
                var sfd = new SaveFileDialog { FileName = single.Name, Title = "Save file to..." };
                if (sfd.ShowDialog() != true) return;
                StatusMessage = $"↓ {single.Name}";
                var ok = await _files.PullAsync(_currentSerial, single.FullPath, sfd.FileName);
                StatusMessage = ok
                    ? $"✅ {LocalizationService.Translate("FileManager.Downloaded")}: {sfd.FileName}"
                    : $"❌ {LocalizationService.Translate("FileManager.DownloadFailed")}";
            }
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task UploadAsync()
    {
        if (!HasDevice) return;
        var ofd = new OpenFileDialog { Title = "Select file to upload", Multiselect = true };
        if (ofd.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            int ok = 0;
            foreach (var file in ofd.FileNames)
            {
                StatusMessage = $"↑ {Path.GetFileName(file)}";
                if (await _files.PushAsync(_currentSerial, file, CurrentPath)) ok++;
            }
            StatusMessage = $"✅ {LocalizationService.Translate("FileManager.Uploaded")} {ok}/{ofd.FileNames.Length}";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task NewFolderAsync()
    {
        if (!HasDevice) return;
        var owner = Application.Current?.MainWindow;
        var name = InputDialogWindow.Show(owner,
            LocalizationService.Translate("FileManager.NewFolder"),
            LocalizationService.Translate("FileManager.NewFolderPrompt"),
            LocalizationService.Translate("FileManager.NewFolder"));

        if (string.IsNullOrWhiteSpace(name)) return;
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');

        var newPath = CurrentPath.TrimEnd('/') + "/" + name;
        IsBusy = true;
        try
        {
            var ok = await _files.CreateFolderAsync(_currentSerial, newPath);
            StatusMessage = ok
                ? $"✅ {LocalizationService.Translate("FileManager.Created")}: {name}"
                : $"❌ {LocalizationService.Translate("FileManager.CreateFailed")}";
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task RenameSelectedAsync()
    {
        if (SelectedItem == null) { StatusMessage = LocalizationService.Translate("FileManager.SelectToRename"); return; }

        var owner = Application.Current?.MainWindow;
        var newName = InputDialogWindow.Show(owner,
            LocalizationService.Translate("FileManager.Rename"),
            LocalizationService.Translate("FileManager.RenamePrompt"),
            SelectedItem.Name);

        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedItem.Name) return;
        foreach (var c in Path.GetInvalidFileNameChars()) newName = newName.Replace(c, '_');

        var parent = FileManagerService.GetParentPath(SelectedItem.FullPath);
        var newPath = parent.TrimEnd('/') + "/" + newName;

        IsBusy = true;
        try
        {
            var ok = await _files.RenameAsync(_currentSerial, SelectedItem.FullPath, newPath);
            StatusMessage = ok
                ? $"✅ {LocalizationService.Translate("FileManager.RenamedTo")}: {newName}"
                : $"❌ {LocalizationService.Translate("FileManager.RenameFailed")}";
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedItems.Count == 0) { StatusMessage = LocalizationService.Translate("FileManager.SelectToDelete"); return; }

        var msg = SelectedItems.Count == 1
            ? string.Format(LocalizationService.Translate("FileManager.ConfirmDeleteMsg"), SelectedItems[0].Name)
            : $"Delete {SelectedItems.Count} items permanently?";

        var confirm = MessageBox.Show(msg,
            LocalizationService.Translate("FileManager.ConfirmDeleteTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        var ok = 0;
        try
        {
            foreach (var item in SelectedItems.ToList())
                if (await _files.DeleteAsync(_currentSerial, item.FullPath)) ok++;

            StatusMessage = ok == SelectedItems.Count
                ? $"✅ {LocalizationService.Translate("FileManager.Deleted")} ({ok})"
                : $"⚠ {ok}/{SelectedItems.Count}";
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class BreadcrumbItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}
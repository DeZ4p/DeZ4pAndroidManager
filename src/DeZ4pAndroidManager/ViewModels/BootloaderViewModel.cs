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
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class BootloaderViewModel : INotifyPropertyChanged
{
    private readonly BootloaderService _svc;
    private readonly DeviceStateService _deviceState;
    private readonly ActivityLogService _activity;

    private DeviceModel? _selectedDevice;
    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private string _currentState = "—";
    private string _deviceInfo = "";
    private string _unlockAbility = "—";
    private string _lastAction = "";

    public BootloaderViewModel(BootloaderService svc, DeviceStateService deviceState,
                               DeviceWatcherService watcher, ActivityLogService activity)
    {
        _svc = svc;
        _deviceState = deviceState;
        _activity = activity;

        RefreshStateCommand = new AsyncRelayCommand(RefreshStateAsync);
        GetAbilityCommand = new AsyncRelayCommand(GetAbilityAsync);
        UnlockCommand = new AsyncRelayCommand(UnlockAsync);
        LockCommand = new AsyncRelayCommand(LockAsync);
        OemUnlockCommand = new AsyncRelayCommand(OemUnlockAsync);
        OemLockCommand = new AsyncRelayCommand(OemLockAsync);
        DeviceInfoCommand = new AsyncRelayCommand(GetDeviceInfoAsync);
        RefreshCommand = new RelayCommand(_ => RefreshDevices());
        SaveReportCommand = new RelayCommand(_ => SaveReport());
        ClearLogCommand = new RelayCommand(_ => ActionLog.Clear());

        watcher.DevicesChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        _deviceState.ConnectionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshDevices);
        RefreshDevices();
    }

    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<string> ActionLog { get; } = new();

    public DeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFastboot)); OnPropertyChanged(nameof(DeviceLabel)); }
    }
    public bool IsFastboot => _selectedDevice?.IsFastboot == true;
    public string DeviceLabel => _selectedDevice?.DisplayName ?? "No device";

    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value ?? ""; OnPropertyChanged(); } }

    public string CurrentState { get => _currentState; set { _currentState = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUnlocked)); OnPropertyChanged(nameof(StateBrush)); } }
    public bool IsUnlocked => CurrentState.IndexOf("unlock", StringComparison.OrdinalIgnoreCase) >= 0
                            && CurrentState.IndexOf("not", StringComparison.OrdinalIgnoreCase) < 0;

    public string StateBrush
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentState) || CurrentState == "—" || CurrentState == "Unknown")
                return "#6B7280";
            return IsUnlocked ? "#EF4444" : "#34D399";
        }
    }

    public string DeviceInfo { get => _deviceInfo; set { _deviceInfo = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasDeviceInfo)); } }
    public bool HasDeviceInfo => !string.IsNullOrEmpty(_deviceInfo);

    public string UnlockAbility { get => _unlockAbility; set { _unlockAbility = value ?? "—"; OnPropertyChanged(); } }
    public string LastAction { get => _lastAction; set { _lastAction = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasLastAction)); } }
    public bool HasLastAction => !string.IsNullOrEmpty(_lastAction);

    public ICommand RefreshStateCommand { get; }
    public ICommand GetAbilityCommand { get; }
    public ICommand UnlockCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand OemUnlockCommand { get; }
    public ICommand OemLockCommand { get; }
    public ICommand DeviceInfoCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SaveReportCommand { get; }
    public ICommand ClearLogCommand { get; }

    private void Log(string msg)
    {
        ActionLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
        while (ActionLog.Count > 50) ActionLog.RemoveAt(ActionLog.Count - 1);
    }

    private void RefreshDevices()
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
        _ = RefreshStateAsync();
    }

    private async Task RefreshStateAsync()
    {
        if (SelectedDevice == null) { CurrentState = "—"; return; }
        try
        {
            var (ok, st) = await _svc.GetUnlockStateAsync(SelectedDevice.Serial);
            CurrentState = ok ? st : "Unknown";
            Log($"State: {CurrentState}");
        }
        catch { CurrentState = "Error"; }
    }

    private async Task GetAbilityAsync()
    {
        if (SelectedDevice == null) return;
        IsBusy = true;
        try
        {
            var (ok, msg) = await _svc.GetUnlockAbilityAsync(SelectedDevice.Serial);
            UnlockAbility = ok ? msg : "Unable to query";
            StatusMessage = ok ? $"Unlock ability: {msg}" : $"Failed: {msg}";
            Log($"Unlock ability: {UnlockAbility}");
        }
        finally { IsBusy = false; }
    }

    private async Task UnlockAsync()
    {
        if (SelectedDevice == null) return;
        var confirm = MessageBox.Show(
            "⚠⚠⚠ BOOTLOADER UNLOCK ⚠⚠⚠\n\n" +
            "This will:\n" +
            "  • Wipe ALL user data (factory reset)\n" +
            "  • Void warranty on some devices\n" +
            "  • Disable SafetyNet / banking apps\n\n" +
            "Type confirmation: Click YES to proceed.",
            "Bootloader Unlock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            StatusMessage = "Sending unlock command...";
            Log("→ flashing unlock");
            var (ok, msg) = await _svc.UnlockAsync(SelectedDevice.Serial);
            LastAction = ok ? "Unlock command sent" : $"Failed: {msg}";
            StatusMessage = LastAction;
            Log(ok ? "✓ Unlock sent" : $"✗ {msg}");
            if (ok) _activity.LogInfo($"Bootloader unlocked: {DeviceLabel}", "UNLOCK", "#EF4444");
        }
        finally { IsBusy = false; }
    }

    private async Task LockAsync()
    {
        if (SelectedDevice == null) return;
        var confirm = MessageBox.Show(
            "⚠ BOOTLOADER LOCK\n\n" +
            "This will:\n" +
            "  • Wipe ALL user data\n" +
            "  • Verify all partitions (may fail if modified)\n\n" +
            "Continue?",
            "Bootloader Lock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            StatusMessage = "Sending lock command...";
            Log("→ flashing lock");
            var (ok, msg) = await _svc.LockAsync(SelectedDevice.Serial);
            LastAction = ok ? "Lock command sent" : $"Failed: {msg}";
            StatusMessage = LastAction;
            Log(ok ? "✓ Lock sent" : $"✗ {msg}");
            if (ok) _activity.LogInfo($"Bootloader locked: {DeviceLabel}", "LOCK", "#34D399");
        }
        finally { IsBusy = false; }
    }

    private async Task OemUnlockAsync()
    {
        if (SelectedDevice == null) return;
        var confirm = MessageBox.Show("Run vendor-specific 'oem unlock'?",
            "OEM Unlock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            Log("→ oem unlock");
            var (ok, msg) = await _svc.OemUnlockAsync(SelectedDevice.Serial);
            LastAction = ok ? "OEM unlock OK" : $"Failed: {msg}";
            StatusMessage = LastAction;
            Log(ok ? "✓ OEM unlock sent" : $"✗ {msg}");
        }
        finally { IsBusy = false; }
    }

    private async Task OemLockAsync()
    {
        if (SelectedDevice == null) return;
        var confirm = MessageBox.Show("Run vendor-specific 'oem lock'?",
            "OEM Lock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        IsBusy = true;
        try
        {
            Log("→ oem lock");
            var (ok, msg) = await _svc.OemLockAsync(SelectedDevice.Serial);
            LastAction = ok ? "OEM lock OK" : $"Failed: {msg}";
            StatusMessage = LastAction;
            Log(ok ? "✓ OEM lock sent" : $"✗ {msg}");
        }
        finally { IsBusy = false; }
    }

    private async Task GetDeviceInfoAsync()
    {
        if (SelectedDevice == null) return;
        IsBusy = true;
        try
        {
            DeviceInfo = await _svc.GetDeviceInfoAsync(SelectedDevice.Serial);
            StatusMessage = "Device info loaded";
            Log("✓ Device info loaded");
        }
        finally { IsBusy = false; }
    }

    private void SaveReport()
    {
        try
        {
            var folder = Path.Combine(AppPaths.Root, "Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"bootloader_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("DeZ4p Bootloader Report");
            sb.AppendLine($"Device: {DeviceLabel}");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"State: {CurrentState}");
            sb.AppendLine($"Unlock ability: {UnlockAbility}");
            sb.AppendLine($"Last action: {LastAction}");
            sb.AppendLine();
            sb.AppendLine("── OEM Device Info ──");
            sb.AppendLine(DeviceInfo);
            sb.AppendLine();
            sb.AppendLine("── Action Log ──");
            foreach (var l in ActionLog) sb.AppendLine(l);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"Saved: {Path.GetFileName(path)}";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
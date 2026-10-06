// Â© DeZ4p | t.me/DeZ4p | All Rights Reserved
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;

namespace DeZ4pAndroidManager.ViewModels;

public class AboutViewModel : INotifyPropertyChanged
{
    public AboutViewModel()
    {
        OpenLinkCommand = new RelayCommand(p => Open((p as string) ?? ""));
        CopyVersionCommand = new RelayCommand(_ => CopyVersion());

        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            Version = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v0.4.2";
        }
        catch { Version = "v0.4.2"; }
    }

    public string AppName => "DeZ4p Android Manager";
    public string Version { get; set; } = "v0.4.2";
    public string Tagline => "Professional Android device management for Windows";
    public string Description =>
        "A complete desktop toolkit for Android device management â€” file manager, " +
        "app manager, backup & restore, screen mirror, logcat viewer, process manager, " +
        "and much more. Works with Android 7 through Android 16, all OEMs.";
    public string DevelopedBy => "Developed by DeZ4p";
    public string ContactTelegram => "t.me/DeZ4p";
    public string Copyright => "Â© 2026 DeZ4p. All Rights Reserved.";

    public ObservableCollection<AboutLink> Links { get; } = new()
    {
        new AboutLink { Label = "Telegram Channel",  Url = "https://t.me/DeZ4p",                                 Icon = "\uE8EA", Color = "#22D3EE" },
        new AboutLink { Label = "GitHub",            Url = "https://github.com/Genymobile/scrcpy",              Icon = "\uE774", Color = "#A855F7" },
        new AboutLink { Label = "ADB Platform Tools",Url = "https://developer.android.com/studio/releases/platform-tools", Icon = "\uE71D", Color = "#34D399" },
        new AboutLink { Label = "scrcpy",            Url = "https://github.com/Genymobile/scrcpy",              Icon = "\uE7F4", Color = "#EC4899" }
    };

    public ICommand OpenLinkCommand { get; }
    public ICommand CopyVersionCommand { get; }

    private void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
    }

    private void CopyVersion()
    {
        try { System.Windows.Clipboard.SetText($"{AppName} {Version}"); } catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Threading.Tasks;
using System.Windows;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;
using DeZ4pAndroidManager.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Unhandled error:\n\n{args.Exception.GetType().Name}: {args.Exception.Message}",
                "DeZ4p Error");
            args.Handled = true;
        };

        base.OnStartup(e);
        try { LocalizationService.Initialize(); } catch { }
        try { ThemeService.ApplySystemTheme(); } catch { }

        var splash = new SplashWindow();
        splash.Show();
        splash.UpdateLayout();

        Dispatcher.BeginInvoke(new Action(async () => await RunStartupSequenceAsync(splash)),
                               System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async Task RunStartupSequenceAsync(SplashWindow splash)
    {
        try
        {
            splash.SetStatus("Starting ADB server...");
            await Task.Delay(250);

            splash.SetStatus("Registering services...");
            var sc = new ServiceCollection();
            ConfigureServices(sc);
            Services = sc.BuildServiceProvider();

            splash.SetStatus("Preparing interface...");
            await Task.Delay(250);

            var mainVm = Services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm, Opacity = 0 };

            splash.SetStatus("Ready.");
            await Task.Delay(150);

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();

            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            mainWindow.BeginAnimation(Window.OpacityProperty, fade);

            Animations.FadeOutAndCollapse(splash.Content as FrameworkElement, () => splash.Close(), 200);

            Services.GetRequiredService<DeviceWatcherService>().Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "DeZ4p Startup Error");
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AdbService>();
        services.AddSingleton<FastbootService>();
        services.AddSingleton<DeviceInfoService>();
        services.AddSingleton<ActivityLogService>();
        services.AddSingleton<AnalyticsService>();
        services.AddSingleton<FileManagerService>();
        services.AddSingleton<DeviceStateService>();
        services.AddSingleton<DeviceWatcherService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<DeviceInfoViewModel>();
        services.AddSingleton<FileManagerViewModel>();
        services.AddSingleton<ApkInstallerViewModel>();
        services.AddSingleton<RebootViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AppsManagerService>();
        services.AddSingleton<AppsManagerViewModel>();
        services.AddSingleton<ContactsService>();
        services.AddSingleton<SmsService>();
        services.AddSingleton<ContactsSmsViewModel>();
        services.AddSingleton<MediaGalleryService>();
        services.AddSingleton<MediaGalleryViewModel>();
        services.AddSingleton<ApkBackupService>();
        services.AddSingleton<ApkBackupViewModel>();
        services.AddSingleton<SplitApkService>();
        services.AddSingleton<SplitApkToolsViewModel>();
        services.AddSingleton<BackupRestoreService>();
        services.AddSingleton<BackupRestoreViewModel>();
        services.AddSingleton<ScreenMirrorService>();
        services.AddSingleton<ScreenMirrorViewModel>();
        services.AddSingleton<ScreenshotRecordService>();
        services.AddSingleton<ScreenshotRecordViewModel>();
        services.AddSingleton<WirelessAdbService>();
        services.AddSingleton<WirelessAdbViewModel>();
        services.AddSingleton<RecoveryManagerService>();
        services.AddSingleton<RecoveryManagerViewModel>();
        services.AddSingleton<ConsoleService>();
        services.AddSingleton<AdbConsoleViewModel>();
        services.AddSingleton<FastbootConsoleViewModel>();
        services.AddSingleton<LogcatService>();
        services.AddSingleton<LogcatViewModel>();
        services.AddSingleton<ProcessService>();
        services.AddSingleton<ProcessManagerViewModel>();
        services.AddSingleton<SensorService>();
        services.AddSingleton<SensorsPanelViewModel>();
        services.AddSingleton<StorageAnalyzerService>();
        services.AddSingleton<StorageAnalyzerViewModel>();
        services.AddSingleton<BatteryLabService>();
        services.AddSingleton<BatteryLabViewModel>();
        services.AddSingleton<NetworkToolsService>();
        services.AddSingleton<NetworkToolsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<SecurityService>();
        services.AddSingleton<SecurityViewModel>();
        services.AddSingleton<ScriptRunnerService>();
        services.AddSingleton<ScriptRunnerViewModel>();
        services.AddSingleton<FlashService>();
        services.AddSingleton<FlashToolsViewModel>();
        services.AddSingleton<BootloaderService>();
        services.AddSingleton<BootloaderViewModel>();
        services.AddSingleton<PartitionService>();
        services.AddSingleton<PartitionToolsViewModel>();
        services.AddSingleton<PermissionsService>();
        services.AddSingleton<PermissionsViewModel>();
        services.AddSingleton<PrivacyService>();
        services.AddSingleton<PrivacyViewModel>();
        services.AddSingleton<TaskSchedulerService>();
        services.AddSingleton<TaskSchedulerViewModel>();
    }
}
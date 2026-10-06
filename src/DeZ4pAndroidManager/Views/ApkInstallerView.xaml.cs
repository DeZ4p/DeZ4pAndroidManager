// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows;
using System.Windows.Controls;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class ApkInstallerView : UserControl
{
    public ApkInstallerView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ApkInstallerViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Animations.FadeInFromBottom(this, 0, 200);
        await ((ApkInstallerViewModel)DataContext).RefreshDevicesAsync();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            ((ApkInstallerViewModel)DataContext).HandleDroppedFile(files[0]);
        }
    }
}
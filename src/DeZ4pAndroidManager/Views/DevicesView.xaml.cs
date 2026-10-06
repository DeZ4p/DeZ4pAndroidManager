// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows;
using System.Windows.Controls;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<DevicesViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Animations.FadeInFromBottom(this, 0, 200);
        await ((DevicesViewModel)DataContext).InitializeAsync();
    }
}
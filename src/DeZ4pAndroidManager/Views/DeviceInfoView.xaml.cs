// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows;
using System.Windows.Controls;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class DeviceInfoView : UserControl
{
    public DeviceInfoView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<DeviceInfoViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Animations.FadeInFromBottom(PageHeader, 0, 200);
        Animations.FadeInFromBottom(HeroCard, 60, 220);
        Animations.FadeInFromBottom(Row1, 140, 240);
        Animations.FadeInFromBottom(Row2, 200, 240);
        Animations.FadeInFromBottom(Row3, 260, 240);
        Animations.FadeInFromBottom(SystemCard, 320, 240);

        await ((DeviceInfoViewModel)DataContext).InitializeAsync();
    }
}
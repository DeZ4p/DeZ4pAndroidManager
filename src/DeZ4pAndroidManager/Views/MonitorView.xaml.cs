// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows;
using System.Windows.Controls;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class MonitorView : UserControl
{
    public MonitorView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MonitorViewModel>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Animations.FadeInFromBottom(this, 0, 220);
        await ((MonitorViewModel)DataContext).InitializeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop polling when leaving the page
        var vm = DataContext as MonitorViewModel;
        vm?.StopCommand.Execute(null);
    }
}
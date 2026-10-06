// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows;
using System.Windows.Controls;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Animations.FadeInFromBottom(PageHeader, 0, 200);
        Animations.FadeInFromBottom(HeroCard, 40, 220);
        Animations.StaggerChildrenFromBottom(StatsRow, 100, 40);
        Animations.FadeInFromBottom(DetailsRow, 180, 240);
        Animations.FadeInFromBottom(AdvancedRow, 240, 240);
        Animations.FadeInFromBottom(AnalyticsSection, 300, 240);
        Animations.FadeInFromBottom(ActivityCard, 360, 240);
        Animations.FadeInFromBottom(SystemCard, 420, 240);

        await ((DashboardViewModel)DataContext).InitializeAsync();
    }
}
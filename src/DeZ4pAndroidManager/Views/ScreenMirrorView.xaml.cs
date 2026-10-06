// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class ScreenMirrorView : UserControl
{
    public ScreenMirrorView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ScreenMirrorViewModel>();
    }
}
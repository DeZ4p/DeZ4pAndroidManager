// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class WirelessAdbView : UserControl
{
    public WirelessAdbView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<WirelessAdbViewModel>();
    }
}
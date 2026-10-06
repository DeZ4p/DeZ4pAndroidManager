// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class RebootView : UserControl
{
    public RebootView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<RebootViewModel>();
        Loaded += async (_, _) => await ((RebootViewModel)DataContext).RefreshDevicesAsync();
    }
}
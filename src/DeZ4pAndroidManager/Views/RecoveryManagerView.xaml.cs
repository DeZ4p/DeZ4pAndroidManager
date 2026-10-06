// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class RecoveryManagerView : UserControl
{
    public RecoveryManagerView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<RecoveryManagerViewModel>();
    }
}
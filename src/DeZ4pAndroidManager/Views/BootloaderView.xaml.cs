using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class BootloaderView : UserControl
{
    public BootloaderView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<BootloaderViewModel>();
    }
}
using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class BatteryLabView : UserControl
{
    public BatteryLabView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<BatteryLabViewModel>();
    }
}
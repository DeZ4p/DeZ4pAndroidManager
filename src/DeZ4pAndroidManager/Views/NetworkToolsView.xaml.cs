using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class NetworkToolsView : UserControl
{
    public NetworkToolsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<NetworkToolsViewModel>();
    }
}
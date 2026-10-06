using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class FlashToolsView : UserControl
{
    public FlashToolsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<FlashToolsViewModel>();
    }
}
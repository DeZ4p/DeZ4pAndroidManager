using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class PermissionsView : UserControl
{
    public PermissionsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PermissionsViewModel>();
    }
}
using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class SecurityView : UserControl
{
    public SecurityView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SecurityViewModel>();
    }
}
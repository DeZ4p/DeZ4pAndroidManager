using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<AboutViewModel>();
    }
}
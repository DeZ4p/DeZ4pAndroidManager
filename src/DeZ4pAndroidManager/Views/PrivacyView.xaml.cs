using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class PrivacyView : UserControl
{
    public PrivacyView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PrivacyViewModel>();
    }
}
using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class StorageAnalyzerView : UserControl
{
    public StorageAnalyzerView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<StorageAnalyzerViewModel>();
    }
}
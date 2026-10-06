using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class PartitionToolsView : UserControl
{
    public PartitionToolsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PartitionToolsViewModel>();
    }
}
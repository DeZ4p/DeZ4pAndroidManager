using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
namespace DeZ4pAndroidManager.Views;
public partial class ScriptRunnerView : UserControl
{
    public ScriptRunnerView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ScriptRunnerViewModel>();
    }
}
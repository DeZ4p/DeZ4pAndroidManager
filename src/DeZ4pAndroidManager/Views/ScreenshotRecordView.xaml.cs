// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class ScreenshotRecordView : UserControl
{
    private ScreenshotRecordViewModel? _vm;

    public ScreenshotRecordView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ScreenshotRecordViewModel>();
        DataContext = _vm;
    }

    private void OnSizeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;
        if (sender is ComboBox cb && cb.SelectedItem is SizePreset sp)
        {
            _vm.RecWidth = sp.Width;
            _vm.RecHeight = sp.Height;
        }
    }
}
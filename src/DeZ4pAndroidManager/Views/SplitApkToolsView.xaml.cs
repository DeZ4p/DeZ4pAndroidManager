// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Windows;
using System.Windows.Controls;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class SplitApkToolsView : UserControl
{
    private SplitApkToolsViewModel? _vm;

    public SplitApkToolsView()
    {
        InitializeComponent();
        try
        {
            _vm = App.Services.GetRequiredService<SplitApkToolsViewModel>();
            DataContext = _vm;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"SplitApkToolsView init failed:\n\n{ex.Message}", "DeZ4p Error");
        }
    }

    private async void OnPrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (_vm == null)
        {
            MessageBox.Show("ViewModel is not available.", "DeZ4p Error");
            return;
        }

        try
        {
            await _vm.PrimaryActionAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Action failed:\n\n{ex.Message}\n\n{ex.StackTrace}", "DeZ4p Error");
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (_vm == null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _vm.HandleDroppedPath(files[0]);
    }
}
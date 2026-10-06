// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class ApkBackupView : UserControl
{
    private ApkBackupViewModel? _vm;

    public ApkBackupView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ApkBackupViewModel>();
        DataContext = _vm;

        var selectAll = new KeyBinding
        {
            Key = Key.A,
            Modifiers = ModifierKeys.Control,
            Command = new RelayCommand(_ => AppListView.SelectAll())
        };
        AppListView.InputBindings.Add(selectAll);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;
        _vm.UpdateSelection(AppListView.SelectedItems.Cast<AppItem>().ToList());
    }
}
// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class AppsManagerView : UserControl
{
    private AppsManagerViewModel? _vm;

    public AppsManagerView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AppsManagerViewModel>();
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
        var selected = AppListView.SelectedItems.Cast<AppItem>().ToList();
        _vm.UpdateSelection(selected);
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) return;
        if (_vm.SelectedItem == null) return;
        if (_vm.LaunchCommand.CanExecute(null))
            _vm.LaunchCommand.Execute(null);
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(AppListView, (DependencyObject)e.OriginalSource) as ListViewItem;
        if (item != null && !item.IsSelected)
        {
            AppListView.SelectedItems.Clear();
            item.IsSelected = true;
        }
    }
}
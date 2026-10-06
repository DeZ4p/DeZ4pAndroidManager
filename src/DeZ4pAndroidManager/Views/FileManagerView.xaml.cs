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

public partial class FileManagerView : UserControl
{
    private FileManagerViewModel? _vm;

    public FileManagerView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<FileManagerViewModel>();
        DataContext = _vm;

        var selectAllBinding = new KeyBinding
        {
            Key = Key.A,
            Modifiers = ModifierKeys.Control,
            Command = new RelayCommand(_ => FileListView.SelectAll())
        };
        FileListView.InputBindings.Add(selectAllBinding);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;
        var selected = FileListView.SelectedItems.Cast<FileItem>().ToList();
        _vm.UpdateSelection(selected);
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null || _vm.SelectedItem == null) return;
        if (_vm.OpenItemCommand.CanExecute(null)) _vm.OpenItemCommand.Execute(null);
    }
}
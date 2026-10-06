// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class MediaGalleryView : UserControl
{
    private MediaGalleryViewModel? _vm;

    public MediaGalleryView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MediaGalleryViewModel>();
        DataContext = _vm;

        var selectAll = new KeyBinding
        {
            Key = Key.A,
            Modifiers = ModifierKeys.Control,
            Command = new RelayCommand(_ => MediaListView.SelectAll())
        };
        MediaListView.InputBindings.Add(selectAll);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null) return;
        _vm.UpdateSelection(MediaListView.SelectedItems.Cast<MediaItem>().ToList());
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null || _vm.SelectedItem == null) return;
        if (_vm.PlayCommand.CanExecute(null)) _vm.PlayCommand.Execute(null);
    }
}
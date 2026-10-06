// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class LogcatViewerView : UserControl
{
    private LogcatViewModel? _vm;

    public LogcatViewerView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<LogcatViewModel>();
        DataContext = _vm;

        if (_vm != null)
            _vm.FilteredEntries.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm == null || !_vm.AutoScroll) return;
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // Find ScrollViewer inside the ListView
            var sv = FindScrollViewer(LogScroll);
            if (sv != null) sv.ScrollToBottom();
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }
}
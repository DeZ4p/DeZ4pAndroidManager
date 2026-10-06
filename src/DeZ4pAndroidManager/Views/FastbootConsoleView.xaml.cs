// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows.Controls;
using System.Windows.Input;
using DeZ4pAndroidManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeZ4pAndroidManager.Views;

public partial class FastbootConsoleView : UserControl
{
    private FastbootConsoleViewModel? _vm;

    public FastbootConsoleView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<FastbootConsoleViewModel>();
        DataContext = _vm;
    }

    private void OnCommandKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm == null) return;
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            if (_vm.RunCommand.CanExecute(null)) _vm.RunCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (_vm.HistoryUpCommand.CanExecute(null)) _vm.HistoryUpCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (_vm.HistoryDownCommand.CanExecute(null)) _vm.HistoryDownCommand.Execute(null);
            e.Handled = true;
        }
    }
}
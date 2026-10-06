// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System.Windows;

namespace DeZ4pAndroidManager.Views;

public partial class InputDialogWindow : Window
{
    public string ResponseText { get; private set; } = "";

    public InputDialogWindow(string title, string prompt, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultValue;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ResponseText = InputBox.Text;
        DialogResult = true;
    }

    public static string Show(Window? owner, string title, string prompt, string defaultValue)
    {
        var dlg = new InputDialogWindow(title, prompt, defaultValue);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.ResponseText : "";
    }
}
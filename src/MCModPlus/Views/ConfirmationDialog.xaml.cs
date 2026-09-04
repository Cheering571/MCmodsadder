using System.Windows;
using Wpf.Ui.Controls;

namespace MCModPlus.Views;

public partial class ConfirmationDialog : FluentWindow
{
    public ConfirmationDialog(string title, string message, string hint = "此操作不可恢复。")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        HintText.Text = hint;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

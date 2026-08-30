using System.Windows;
using Wpf.Ui.Controls;

namespace McModsAdder.Views;

public partial class InputDialog : FluentWindow
{
    public string InputText => InputBox.Text;

    public InputDialog(string title, string label, string defaultText = "")
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        InputBox.Text = defaultText;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
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

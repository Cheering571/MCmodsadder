using System.Windows;
using MCModPlus.ViewModels;
using Wpf.Ui.Controls;

namespace MCModPlus.Views;

public partial class LocalModPickerWindow : FluentWindow
{
    public LocalModPickerWindow(ProfileEditorViewModel editor)
    {
        InitializeComponent();
        DataContext = new LocalModPickerViewModel(editor);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MCModPlus;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class ProfileEditorPage : Page, INavigatedTo
{
    private readonly ProfileEditorViewModel _vm;

    public ProfileEditorPage(ProfileEditorViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OpenLocalModLibrary_Click(object sender, RoutedEventArgs e)
    {
        var window = new LocalModPickerWindow(_vm)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void SearchResultsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _vm.UpdatePageSize(e.NewSize.Height);
    }
}

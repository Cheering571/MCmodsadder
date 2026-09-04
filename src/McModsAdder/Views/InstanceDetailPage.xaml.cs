using System.Windows;
using System.Windows.Controls;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class InstanceDetailPage : Page, INavigatedTo
{
    private readonly InstanceDetailViewModel _vm;

    public InstanceDetailPage(InstanceDetailViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _ = _vm.InitializeAsync();

    private void InstalledModsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasRecognizedMods)
        {
            return;
        }

        var window = new InstalledModsWindow(_vm.InstalledMods)
        {
            Owner = Window.GetWindow(this)
        };
        window.Show();
    }
}

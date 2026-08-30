using System.Windows.Controls;
using McModsAdder.Services;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

public partial class InstancesPage : Page, INavigatedTo
{
    private readonly InstancesViewModel _vm;

    public InstancesPage(InstancesViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.ScanDefault();
}

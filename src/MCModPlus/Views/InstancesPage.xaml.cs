using System.Windows.Controls;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

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

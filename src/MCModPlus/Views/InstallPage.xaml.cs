using System.Windows.Controls;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class InstallPage : Page, INavigatedTo
{
    private readonly InstallViewModel _vm;

    public InstallPage(InstallViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();
}

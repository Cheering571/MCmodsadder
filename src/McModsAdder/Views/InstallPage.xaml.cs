using System.Windows.Controls;
using McModsAdder.Services;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

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

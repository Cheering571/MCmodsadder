using System.Windows.Controls;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class HomePage : Page, INavigatedTo
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();
}

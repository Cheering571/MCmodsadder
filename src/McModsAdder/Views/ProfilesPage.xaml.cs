using System.Windows.Controls;
using McModsAdder.Services;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

public partial class ProfilesPage : Page, INavigatedTo
{
    private readonly ProfilesViewModel _vm;

    public ProfilesPage(ProfilesViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();
}

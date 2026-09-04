using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class ProfilesPage : Page, INavigatedTo, INavigatedFrom
{
    private readonly ProfilesViewModel _vm;

    public ProfilesPage(ProfilesViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();

    public void OnNavigatedFrom() => _vm.CancelPendingDelete();

    private void OnPagePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindDeleteAction(e.OriginalSource as DependencyObject) is null) _vm.CancelPendingDelete();
    }

    private static Button? FindDeleteAction(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button { Tag: "DeleteAction" }) return element as Button;
            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}

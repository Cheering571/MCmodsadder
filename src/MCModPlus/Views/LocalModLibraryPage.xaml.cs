using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MCModPlus.Models;
using MCModPlus.Services;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class LocalModLibraryPage : Page, INavigatedTo, INavigatedFrom
{
    private readonly LocalModLibraryViewModel _vm;

    public LocalModLibraryPage(LocalModLibraryViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();

    public void OnNavigatedFrom() => _vm.CancelPendingDelete();

    private void LoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: LocalMod mod } comboBox
            && comboBox.SelectedItem is ModLoader loader)
        {
            _vm.SetLoader(mod, loader);
        }
    }

    private void OnPagePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindDeleteAction(e.OriginalSource as DependencyObject) is null) _vm.CancelPendingDelete();
    }

    private static Button? FindDeleteAction(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button { Tag: "DeleteAction" or "BatchDeleteAction" } button) return button;
            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}

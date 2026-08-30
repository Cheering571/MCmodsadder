using System.Windows.Controls;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}

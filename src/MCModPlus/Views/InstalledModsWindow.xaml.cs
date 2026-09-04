using System.Windows;
using System.Windows.Threading;
using MCModPlus.Models;
using MCModPlus.ViewModels;
using Wpf.Ui.Controls;

namespace MCModPlus.Views;

public partial class InstalledModsWindow : FluentWindow
{
    private readonly IEnumerable<InstalledMod> _mods;

    public InstalledModsWindow(IEnumerable<InstalledMod> mods)
    {
        _mods = mods;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            DataContext = new InstalledModsViewModel(_mods);
        }));
    }
}

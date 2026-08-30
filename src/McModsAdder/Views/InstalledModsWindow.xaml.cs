using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using McModsAdder.Models;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

public partial class InstalledModsWindow : Window
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

    private void WindowDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || ReferenceEquals(source, CloseButton)
            || IsInsideElement(source, CloseButton)
            || IsInsideExcludedArea(source)
            || source is System.Windows.Controls.Control)
        {
            return;
        }

        DragMove();
        e.Handled = true;
    }

    private static bool IsInsideElement(DependencyObject source, DependencyObject element)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, element))
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private bool IsInsideExcludedArea(DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, SearchBorder) || ReferenceEquals(source, ModsListBorder))
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

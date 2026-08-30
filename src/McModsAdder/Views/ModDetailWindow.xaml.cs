using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using McModsAdder.Services;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

public partial class ModDetailWindow : UserControl
{

    public ModDetailWindow()
    {
        InitializeComponent();
    }

    public ModDetailWindow(ModSearchResult result) : this()
    {
        DataContext = new ModDetailViewModel(result);
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && Uri.TryCreate(element.Tag as string, UriKind.Absolute, out var uri))
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
    }
}

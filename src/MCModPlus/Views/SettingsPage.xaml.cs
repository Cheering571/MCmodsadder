using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MCModPlus.ViewModels;

namespace MCModPlus.Views;

public partial class SettingsPage : Page
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Cheering571/MCModPlus",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开 GitHub 链接：{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private void OnPagePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindCurseForgeAction(e.OriginalSource as DependencyObject) is null)
        {
            _vm.CancelPendingCurseForgeAction();
        }
    }

    private static DependencyObject? FindCurseForgeAction(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button { Tag: "CurseForgeApiEditAction" or "CurseForgeApiClearAction" } button)
            {
                return button;
            }

            if (element is TextBox { Tag: "CurseForgeApiInput" } or PasswordBox { Tag: "CurseForgeApiInput" })
            {
                return element;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}

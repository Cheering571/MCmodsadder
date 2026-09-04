using System.Windows;
using MCModPlus.Models;
using Wpf.Ui.Controls;

namespace MCModPlus.Views;

public partial class LoaderSelectionDialog : FluentWindow
{
    public ModLoader SelectedLoader => LoaderComboBox.SelectedItem is ModLoader loader ? loader : ModLoader.Unknown;

    public LoaderSelectionDialog(int count)
    {
        InitializeComponent();
        Title = "批量修改加载器";
        DescriptionText.Text = $"为选中的 {count} 个 Mod 设置加载器";
        LoaderComboBox.ItemsSource = Enum.GetValues<ModLoader>().Where(loader => loader != ModLoader.Unknown).ToList();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (SelectedLoader == ModLoader.Unknown) return;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

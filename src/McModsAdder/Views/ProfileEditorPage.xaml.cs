using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using McModsAdder;
using McModsAdder.Services;
using McModsAdder.ViewModels;

namespace McModsAdder.Views;

public partial class ProfileEditorPage : Page, INavigatedTo
{
    private readonly ProfileEditorViewModel _vm;

    public ProfileEditorPage(ProfileEditorViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    public void OnNavigatedTo() => _vm.LoadData();

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void SearchResultsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _vm.UpdatePageSize(e.NewSize.Height);
    }

    private void ModItem_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { ToolTip: ToolTip toolTip })
        {
            toolTip.IsOpen = false;
        }
    }

    private void ModItem_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        UpdateToolTipPosition(sender as FrameworkElement);
    }

    private void ModItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdateToolTipPosition(element, e.GetPosition(element));
        }
    }

    private static void UpdateToolTipPosition(FrameworkElement? element, Point? position = null)
    {
        if (element?.ToolTip is not ToolTip toolTip)
        {
            return;
        }

        var pointerPosition = position ?? Mouse.GetPosition(element);
        toolTip.PlacementRectangle = new Rect(pointerPosition, new Size(0, 0));
        toolTip.HorizontalOffset = 16;
        toolTip.VerticalOffset = 16;
    }

    private void SearchResult_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<Control>(source) is Button)
        {
            return;
        }

        if (sender is FrameworkElement element)
        {
            if (element.DataContext is SearchResultItem item)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.ShowModDetail(item.Result);
                e.Handled = true;
            }
            else if (element.DataContext is ProfileEntryItem entryItem)
            {
                var result = new ModSearchResult
                {
                    ProjectId = entryItem.Entry.ProjectId,
                    Slug = entryItem.Entry.Slug,
                    Name = entryItem.Entry.Name,
                    IconUrl = entryItem.Entry.IconUrl,
                    Summary = entryItem.Entry.Summary,
                    Author = entryItem.Entry.Author,
                    Downloads = entryItem.Entry.Downloads
                };
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.ShowModDetail(result);
                e.Handled = true;
            }
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match)
            {
                return match;
            }
            child = LogicalTreeHelper.GetParent(child) ?? VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

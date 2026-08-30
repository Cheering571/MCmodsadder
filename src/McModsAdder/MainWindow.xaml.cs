using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using McModsAdder.Helpers;
using McModsAdder.Services;
using McModsAdder.ViewModels;
using McModsAdder.Views;
using Wpf.Ui.Controls;

namespace McModsAdder;

public partial class MainWindow : FluentWindow
{
    private readonly NavigationService _nav;
    private Page? _currentPage;
    private bool _initialNavigationCompleted;

    public MainWindow(NavigationService nav)
    {
        _nav = nav;
        InitializeComponent();

        NavView.SetServiceProvider(App.Services);
        NavView.Navigated += OnNavigated;
        NavView.SizeChanged += (_, _) => ApplyPageConstraint();
        _nav.Initialize(NavView);
        Loaded += OnLoaded;

        // 滚轮重定向：焦点不在内容区时也能用滚轮滚动鼠标下方的滚动区
        PreviewMouseWheel += ScrollHelper.HandleWheel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialNavigationCompleted)
        {
            return;
        }

        _initialNavigationCompleted = true;
        _nav.Navigate<InstancesPage>();
        Dispatcher.BeginInvoke(DisableNavigationHostScrollViewer);
    }

    private void DisableNavigationHostScrollViewer()
    {
        foreach (var viewer in FindVisualChildren<ScrollViewer>(NavView))
        {
            viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        if (args.Page is Page page)
        {
            _currentPage = page;
            ApplyPageConstraint();
            Dispatcher.BeginInvoke(DisableNavigationHostScrollViewer);
            if (page is INavigatedTo aware)
            {
                aware.OnNavigatedTo();
            }
        }
    }

    private Point _detailDragStartScreen;
    private double _detailStartHorizontalOffset;
    private double _detailStartVerticalOffset;

    public void ShowModDetail(ModSearchResult result)
    {
        ModDetailPanel.DataContext = new ModDetailViewModel(result);
        DetailPopup.HorizontalOffset = 0;
        DetailPopup.VerticalOffset = 0;
        DetailPopup.IsOpen = true;
        ModDetailPanel.Focus();
    }

    private void DetailPopup_Closed(object? sender, EventArgs e)
    {
        ModDetailPanel.ReleaseMouseCapture();
    }

    private void ModDetailPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _detailDragStartScreen = ModDetailPanel.PointToScreen(e.GetPosition(ModDetailPanel));
        _detailStartHorizontalOffset = DetailPopup.HorizontalOffset;
        _detailStartVerticalOffset = DetailPopup.VerticalOffset;
        ModDetailPanel.CaptureMouse();
        e.Handled = true;
    }

    private void ModDetailPanel_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!ModDetailPanel.IsMouseCaptured)
        {
            return;
        }

        var currentScreen = ModDetailPanel.PointToScreen(e.GetPosition(ModDetailPanel));
        var deltaX = currentScreen.X - _detailDragStartScreen.X;
        var deltaY = currentScreen.Y - _detailDragStartScreen.Y;
        DetailPopup.HorizontalOffset = _detailStartHorizontalOffset + deltaX;
        DetailPopup.VerticalOffset = _detailStartVerticalOffset + deltaY;
    }

    private void ModDetailPanel_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ModDetailPanel.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>
    /// NavigationView 的 Frame 宿主不按视口高度约束页面，
    /// 会导致页面按无限高度测量、内部滚动区失效（外层连带头部整体滚动）。
    /// 显式把页面最大高度限制为导航视图实际高度，使内部滚动区生效。
    /// </summary>
    private void ApplyPageConstraint()
    {
        if (_currentPage != null && NavView.ActualHeight > 0)
        {
            _currentPage.MaxHeight = NavView.ActualHeight;
            _currentPage.MaxWidth = Math.Max(0, NavView.ActualWidth - NavView.OpenPaneLength);
        }
    }
}

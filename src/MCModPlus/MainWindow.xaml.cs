using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MCModPlus.Helpers;
using MCModPlus.Services;
using MCModPlus.ViewModels;
using MCModPlus.Views;
using Wpf.Ui.Controls;

namespace MCModPlus;

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
        PreviewMouseDown += OnWindowPreviewMouseDown;
    }

    private void OnWindowPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.GetPosition(NavView).X <= NavView.OpenPaneLength
            && _currentPage is INavigatedFrom page)
        {
            page.OnNavigatedFrom();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialNavigationCompleted)
        {
            return;
        }

        _initialNavigationCompleted = true;
        _nav.Navigate<HomePage>();
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
        if (_currentPage is INavigatedFrom previousPage)
        {
            previousPage.OnNavigatedFrom();
        }

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

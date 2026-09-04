using Wpf.Ui.Controls;

namespace MCModPlus.Services;

public interface INavigatedTo
{
    void OnNavigatedTo();
}

public interface INavigatedFrom
{
    void OnNavigatedFrom();
}

/// <summary>
/// 页面导航服务：包装 WPF-UI NavigationView 的内置导航。
/// </summary>
public class NavigationService
{
    private NavigationView? _navView;

    public void Initialize(NavigationView navView)
    {
        _navView = navView;
    }

    public void Navigate<TPage>() where TPage : class
    {
        _navView?.Navigate(typeof(TPage));
    }
}

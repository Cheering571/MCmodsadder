using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;
using MCModPlus.Services;
using MCModPlus.Views;
using Microsoft.Win32;

namespace MCModPlus.ViewModels;

public enum InstanceSortMode
{
    Name,
    GameVersion,
    Loader
}

public partial class InstancesViewModel : ObservableObject
{
    private readonly InstanceScanner _scanner;
    private readonly SettingsService _settings;
    private readonly AppState _appState;
    private readonly NavigationService _nav;

    [ObservableProperty]
    private ObservableCollection<GameInstance> _instances = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasInstances;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedLoader = "全部加载器";

    [ObservableProperty]
    private string _selectedGameVersion = "全部版本";

    [ObservableProperty]
    private string _sortMode = "MC 版本";

    [ObservableProperty]
    private bool _showVanilla;

    public ICollectionView InstancesView { get; }

    public IReadOnlyList<string> LoaderOptions =>
        new[] { "全部加载器" }
            .Concat(Instances
                .Select(i => i.Loader.ToDisplay())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(loader => loader, StringComparer.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<string> GameVersionOptions =>
        new[] { "全部版本" }.Concat(Instances
            .Where(i => !string.IsNullOrWhiteSpace(i.GameVersion))
            .OrderBy(i => i.GameVersionSortKey)
            .Select(i => i.GameVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<string> SortModes { get; } = new[] { "名称", "MC 版本", "加载器" };

    public int VanillaInstanceCount => Instances.Count(instance => !instance.IsModded);

    public InstancesViewModel(InstanceScanner scanner, SettingsService settings, AppState appState, NavigationService nav)
    {
        _scanner = scanner;
        _settings = settings;
        _appState = appState;
        _nav = nav;
        InstancesView = CollectionViewSource.GetDefaultView(Instances);
        InstancesView.Filter = FilterInstance;
        ApplySort();
    }

    partial void OnFilterTextChanged(string value)
    {
        InstancesView.Refresh();
        UpdateHasInstances();
    }

    partial void OnSelectedLoaderChanged(string value)
    {
        InstancesView.Refresh();
        UpdateHasInstances();
    }

    partial void OnSelectedGameVersionChanged(string value)
    {
        InstancesView.Refresh();
        UpdateHasInstances();
    }

    partial void OnShowVanillaChanged(bool value)
    {
        InstancesView.Refresh();
        UpdateHasInstances();
    }

    partial void OnSortModeChanged(string value) => ApplySort();

    private bool FilterInstance(object item)
    {
        if (item is not GameInstance instance)
        {
            return false;
        }

        if (_settings.IsExcluded(instance.DirectoryPath)
            || (!ShowVanilla && !instance.IsModded))
        {
            return false;
        }

        var text = FilterText.Trim();
        var matchesText = string.IsNullOrWhiteSpace(text)
            || instance.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || instance.DirectoryPath.Contains(text, StringComparison.OrdinalIgnoreCase);
        var matchesLoader = SelectedLoader == "全部加载器"
            || instance.Loader.ToDisplay() == SelectedLoader;
        var matchesVersion = SelectedGameVersion == "全部版本"
            || string.Equals(instance.GameVersion, SelectedGameVersion, StringComparison.OrdinalIgnoreCase);
        return matchesText && matchesLoader && matchesVersion;
    }

    private void UpdateHasInstances()
    {
        HasInstances = InstancesView.Cast<GameInstance>().Any();
    }

    private void ApplySort()
    {
        using (InstancesView.DeferRefresh())
        {
            InstancesView.SortDescriptions.Clear();
            InstancesView.GroupDescriptions.Clear();

            var sortProperty = SortMode switch
            {
                "MC 版本" => nameof(GameInstance.GameVersionSortKey),
                "加载器" => nameof(GameInstance.Loader),
                _ => nameof(GameInstance.Name)
            };
            InstancesView.SortDescriptions.Add(new SortDescription(sortProperty, ListSortDirection.Ascending));
            InstancesView.SortDescriptions.Add(new SortDescription(nameof(GameInstance.Name), ListSortDirection.Ascending));

            if (SortMode == "MC 版本")
            {
                InstancesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GameInstance.GameVersion)));
            }
            else if (SortMode == "加载器")
            {
                InstancesView.GroupDescriptions.Add(new PropertyGroupDescription(sortProperty));
            }
        }
    }

    private void RefreshFilterOptions()
    {
        OnPropertyChanged(nameof(LoaderOptions));
        OnPropertyChanged(nameof(GameVersionOptions));

        if (SelectedLoader != "全部加载器" && !LoaderOptions.Contains(SelectedLoader))
        {
            SelectedLoader = "全部加载器";
        }

        if (SelectedGameVersion != "全部版本" && !GameVersionOptions.Contains(SelectedGameVersion))
        {
            SelectedGameVersion = "全部版本";
        }
    }

    public void ScanDefault()
    {
        var instances = _scanner.ScanAll(_settings.ScanRoots)
            .Where(instance => !_settings.IsExcluded(instance.DirectoryPath))
            .ToList();
        ReplaceInstances(instances);

        StatusText = instances.Count > 0
            ? $"共发现 {instances.Count} 个实例"
            : "未找到游戏实例，请点击「选择整合包目录」添加自定义目录";
    }

    [RelayCommand]
    private void Rescan() => ScanDefault();

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择整合包目录（.minecraft、versions 或实例目录均可）"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var path = dialog.FolderName;
        _settings.AddScanRoot(path);
        ReplaceInstances(_scanner.ScanAll(_settings.ScanRoots)
            .Where(instance => !_settings.IsExcluded(instance.DirectoryPath)));

        if (Instances.Count == 0)
        {
            StatusText = "所选目录下未识别到实例，请确认选择了正确的整合包目录";
        }
    }

    private void ReplaceInstances(IEnumerable<GameInstance> instances)
    {
        Instances.Clear();
        foreach (var instance in instances)
        {
            Instances.Add(instance);
        }
        OnPropertyChanged(nameof(VanillaInstanceCount));

        RefreshFilterOptions();
        InstancesView.Refresh();
        UpdateHasInstances();
        StatusText = Instances.Count > 0
            ? $"共发现 {Instances.Count} 个实例"
            : StatusText;
    }

    [RelayCommand]
    private void DeleteInstance(GameInstance instance)
    {
        if (instance == null) return;

        _settings.ExcludeInstance(instance.DirectoryPath);
        Instances.Remove(instance);
        OnPropertyChanged(nameof(VanillaInstanceCount));
        RefreshFilterOptions();
        InstancesView.Refresh();
        HasInstances = InstancesView.Cast<GameInstance>().Any();
    }

    [RelayCommand]
    private void OpenInstance(GameInstance instance)
    {
        _appState.CurrentInstance = instance;
        _nav.Navigate<InstanceDetailPage>();
    }
}

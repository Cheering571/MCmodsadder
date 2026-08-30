using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McModsAdder.Models;
using McModsAdder.Services;
using McModsAdder.Views;
using Microsoft.Win32;

namespace McModsAdder.ViewModels;

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
    private string _sortMode = "名称";

    public ICollectionView InstancesView { get; }

    public IReadOnlyList<string> LoaderOptions { get; } = new[]
    {
        "全部加载器", "Fabric", "Forge", "Quilt", "NeoForge", "未知"
    };

    public IReadOnlyList<string> GameVersionOptions =>
        new[] { "全部版本" }.Concat(Instances.Select(i => i.GameVersion)
            .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<string> SortModes { get; } = new[] { "名称", "MC 版本", "Mod 加载器" };

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

    partial void OnFilterTextChanged(string value) => InstancesView.Refresh();

    partial void OnSelectedLoaderChanged(string value) => InstancesView.Refresh();

    partial void OnSelectedGameVersionChanged(string value) => InstancesView.Refresh();

    partial void OnSortModeChanged(string value) => ApplySort();

    private bool FilterInstance(object item)
    {
        if (item is not GameInstance instance)
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

    private void ApplySort()
    {
        using (InstancesView.DeferRefresh())
        {
            InstancesView.SortDescriptions.Clear();
            InstancesView.GroupDescriptions.Clear();

            var sortProperty = SortMode switch
            {
                "MC 版本" => nameof(GameInstance.GameVersion),
                "Mod 加载器" => nameof(GameInstance.Loader),
                _ => nameof(GameInstance.Name)
            };
            InstancesView.SortDescriptions.Add(new SortDescription(sortProperty, ListSortDirection.Ascending));
            InstancesView.SortDescriptions.Add(new SortDescription(nameof(GameInstance.Name), ListSortDirection.Ascending));

            if (SortMode is "MC 版本" or "Mod 加载器")
            {
                InstancesView.GroupDescriptions.Add(new PropertyGroupDescription(sortProperty));
            }
        }
    }

    private void RefreshVersionOptions()
    {
        OnPropertyChanged(nameof(GameVersionOptions));
        if (SelectedGameVersion != "全部版本" && !GameVersionOptions.Contains(SelectedGameVersion))
        {
            SelectedGameVersion = "全部版本";
        }
    }

    public void ScanDefault()
    {
        var roots = new List<string>();
        var defaultDir = InstanceScanner.GetDefaultMinecraftDir();
        if (defaultDir != null)
        {
            roots.Add(defaultDir);
        }
        roots.AddRange(_settings.ScanRoots);
        ScanRoots(roots.Distinct().ToList());

        if (roots.Count == 0)
        {
            StatusText = "未找到默认 .minecraft 目录，请点击「选择目录」手动指定整合包位置";
        }
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
        ScanRoots(new List<string> { path });

        if (Instances.Count == 0)
        {
            StatusText = "所选目录下未识别到实例，请确认选择了正确的整合包目录";
        }
    }

    private void ScanRoots(List<string> roots)
    {
        var found = new Dictionary<string, GameInstance>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            foreach (var inst in _scanner.Scan(root))
            {
                found[inst.DirectoryPath] = inst;
            }
        }

        Instances.Clear();
        foreach (var instance in found.Values)
        {
            Instances.Add(instance);
        }
        RefreshVersionOptions();
        InstancesView.Refresh();
        HasInstances = Instances.Count > 0;
        StatusText = Instances.Count > 0
            ? $"共发现 {Instances.Count} 个实例"
            : StatusText;
    }

    [RelayCommand]
    private void OpenInstance(GameInstance instance)
    {
        _appState.CurrentInstance = instance;
        _nav.Navigate<InstanceDetailPage>();
    }
}

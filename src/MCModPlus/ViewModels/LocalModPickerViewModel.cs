using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;

namespace MCModPlus.ViewModels;

public partial class LocalModPickerItem : ObservableObject
{
    public LocalMod Mod { get; }

    public string Name => Mod.Name;
    public ModLoader Loader => Mod.Loader;
    public string GameVersionSortKey => Mod.GameVersionSortKey;

    [ObservableProperty]
    private bool _isAdded;

    public LocalModPickerItem(LocalMod mod, bool isAdded)
    {
        Mod = mod;
        IsAdded = isAdded;
    }
}

public partial class LocalModPickerViewModel : ObservableObject
{
    private readonly ProfileEditorViewModel _editor;

    public ObservableCollection<LocalModPickerItem> Mods { get; } = new();

    public ICollectionView ModsView { get; }

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedLoader = "全部加载器";

    [ObservableProperty]
    private string _selectedGameVersion = "全部版本";

    [ObservableProperty]
    private string _sortMode = "名称";

    public IReadOnlyList<string> LoaderFilterOptions =>
        new[] { "全部加载器" }
            .Concat(Mods.Select(item => item.Mod.Loader.ToDisplay()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<string> GameVersionOptions =>
        new[] { "全部版本" }
            .Concat(Mods.Select(item => item.Mod.GameVersion).Where(value => !string.IsNullOrWhiteSpace(value) && value != "未知").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(GameVersionSortKey, StringComparer.Ordinal))
            .ToList();

    public IReadOnlyList<string> SortModes { get; } = new[] { "名称", "MC 版本", "Mod 加载器" };

    public string CountText => $"本地库共 {Mods.Count} 个 Mod";

    public LocalModPickerViewModel(ProfileEditorViewModel editor)
    {
        _editor = editor;
        foreach (var mod in editor.LocalMods)
        {
            Mods.Add(new LocalModPickerItem(mod, editor.IsLocalModAdded(mod)));
        }

        ModsView = CollectionViewSource.GetDefaultView(Mods);
        ModsView.Filter = FilterMod;
        ApplySort();
    }

    partial void OnFilterTextChanged(string value) => ModsView.Refresh();
    partial void OnSelectedLoaderChanged(string value) => ModsView.Refresh();
    partial void OnSelectedGameVersionChanged(string value) => ModsView.Refresh();
    partial void OnSortModeChanged(string value) => ApplySort();

    [RelayCommand]
    private void Add(LocalModPickerItem item)
    {
        if (item.IsAdded) return;
        _editor.AddLocalModCommand.Execute(item.Mod);
        item.IsAdded = _editor.IsLocalModAdded(item.Mod);
    }

    private bool FilterMod(object item)
    {
        if (item is not LocalModPickerItem picker) return false;
        var mod = picker.Mod;
        var text = FilterText.Trim();
        return (string.IsNullOrWhiteSpace(text)
                || mod.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || mod.FileName.Contains(text, StringComparison.OrdinalIgnoreCase))
            && (SelectedLoader == "全部加载器" || mod.Loader.ToDisplay() == SelectedLoader)
            && (SelectedGameVersion == "全部版本" || string.Equals(mod.GameVersion, SelectedGameVersion, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplySort()
    {
        using (ModsView.DeferRefresh())
        {
            ModsView.SortDescriptions.Clear();
            var property = SortMode switch
            {
                "MC 版本" => nameof(LocalModPickerItem.GameVersionSortKey),
                "Mod 加载器" => nameof(LocalModPickerItem.Loader),
                _ => nameof(LocalModPickerItem.Name)
            };
            ModsView.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Ascending));
        }
    }

    private static string GameVersionSortKey(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('.', parts.Select(part => int.TryParse(part, out var number) ? number.ToString("D5") : part));
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;
using MCModPlus.Services;
using Microsoft.Win32;

namespace MCModPlus.ViewModels;

public partial class LocalModLibraryViewModel : ObservableObject
{
    private readonly LocalModLibraryService _library;
    private bool _suppressAllSelectionUpdate;

    [ObservableProperty]
    private ObservableCollection<LocalMod> _mods = new();

    [ObservableProperty]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private int _addedCount;

    [ObservableProperty]
    private int _duplicateCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private bool _isMessageVisible;

    private DispatcherTimer? _messageTimer;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedLoader = "全部加载器";

    [ObservableProperty]
    private string _selectedGameVersion = "全部版本";

    [ObservableProperty]
    private string _sortMode = "加载器";

    [ObservableProperty]
    private bool _isAllSelected;

    [ObservableProperty]
    private bool _isBatchDeletePending;

    public int SelectedCount => Mods.Count(mod => mod.IsSelected);

    public bool HasSelection => SelectedCount > 0;

    public string SelectionText => HasSelection ? $"已选 {SelectedCount} 个 Mod" : "尚未选择 Mod";

    public int ModCount => Mods.Count;

    public ICollectionView ModsView { get; }

    public IReadOnlyList<string> LoaderFilterOptions =>
        new[] { "全部加载器" }
            .Concat(Mods.Select(mod => mod.Loader.ToDisplay())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(loader => loader, StringComparer.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<string> GameVersionOptions =>
        new[] { "全部版本" }
            .Concat(Mods.Select(mod => mod.GameVersion)
                .Where(version => !string.IsNullOrWhiteSpace(version) && version != "未知")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GameVersionSortKey, StringComparer.Ordinal))
            .ToList();

    public IReadOnlyList<string> SortModes { get; } = new[] { "名称", "MC 版本", "加载器" };

    public IReadOnlyList<ModLoader> LoaderOptions { get; } = Enum.GetValues<ModLoader>();

    public LocalModLibraryViewModel(LocalModLibraryService library)
    {
        _library = library;
        ModsView = CollectionViewSource.GetDefaultView(Mods);
        ModsView.Filter = FilterMod;
        ApplySort();
    }

    partial void OnFilterTextChanged(string value) => ModsView.Refresh();
    partial void OnSelectedLoaderChanged(string value) => ModsView.Refresh();
    partial void OnSelectedGameVersionChanged(string value) => ModsView.Refresh();
    partial void OnSortModeChanged(string value) => ApplySort();
    partial void OnIsAllSelectedChanged(bool value)
    {
        if (_suppressAllSelectionUpdate) return;
        _suppressAllSelectionUpdate = true;
        try
        {
            foreach (var mod in ModsView.Cast<LocalMod>()) mod.IsSelected = value;
        }
        finally
        {
            _suppressAllSelectionUpdate = false;
        }
        RefreshSelectionState();
    }

    private void OnModPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalMod.IsSelected)) RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionText));
        var visibleMods = ModsView.Cast<LocalMod>().ToList();
        var shouldBeAllSelected = visibleMods.Count > 0 && visibleMods.All(mod => mod.IsSelected);
        if (IsAllSelected != shouldBeAllSelected)
        {
            _suppressAllSelectionUpdate = true;
            try
            {
                IsAllSelected = shouldBeAllSelected;
            }
            finally
            {
                _suppressAllSelectionUpdate = false;
            }
        }
    }

    private IReadOnlyList<LocalMod> SelectedMods => Mods.Where(mod => mod.IsSelected).ToList();

    private bool FilterMod(object item)
    {
        if (item is not LocalMod mod) return false;
        var text = FilterText.Trim();
        return (string.IsNullOrWhiteSpace(text)
                || mod.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || mod.FileName.Contains(text, StringComparison.OrdinalIgnoreCase))
            && (SelectedLoader == "全部加载器" || mod.Loader.ToDisplay() == SelectedLoader)
            && (SelectedGameVersion == "全部版本"
                || string.Equals(mod.GameVersion, SelectedGameVersion, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplySort()
    {
        using (ModsView.DeferRefresh())
        {
            ModsView.SortDescriptions.Clear();
            ModsView.GroupDescriptions.Clear();
            var property = SortMode switch
            {
                "MC 版本" => nameof(LocalMod.GameVersionSortKey),
                "加载器" => nameof(LocalMod.Loader),
                _ => nameof(LocalMod.Name)
            };
            ModsView.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Ascending));
            ModsView.SortDescriptions.Add(new SortDescription(nameof(LocalMod.Name), ListSortDirection.Ascending));
            if (SortMode == "MC 版本")
            {
                ModsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LocalMod.GameVersion)));
            }
            else if (SortMode == "加载器")
            {
                ModsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LocalMod.Loader)));
            }
        }
    }

    private void RefreshFilterOptions()
    {
        OnPropertyChanged(nameof(LoaderFilterOptions));
        OnPropertyChanged(nameof(GameVersionOptions));
        if (SelectedLoader != "全部加载器" && !LoaderFilterOptions.Contains(SelectedLoader)) SelectedLoader = "全部加载器";
        if (SelectedGameVersion != "全部版本" && !GameVersionOptions.Contains(SelectedGameVersion)) SelectedGameVersion = "全部版本";
    }

    private void ShowMessage(int addedCount, int duplicateCount, int failedCount)
    {
        AddedCount = addedCount;
        DuplicateCount = duplicateCount;
        FailedCount = failedCount;
        MessageText = "本地 Mod 添加完成";
        IsMessageVisible = addedCount > 0 || duplicateCount > 0 || failedCount > 0;
        _messageTimer?.Stop();
        if (!IsMessageVisible) return;
        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            IsMessageVisible = false;
        };
        _messageTimer.Start();
    }
    private static string GameVersionSortKey(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('.', parts.Select(part => int.TryParse(part, out var number) ? number.ToString("D5") : part));
    }

    public void LoadData()
    {
        _library.Load();
        Mods.Clear();
        foreach (var mod in _library.Mods)
        {
            mod.IsSelected = false;
            mod.IsDeletePending = false;
            mod.PropertyChanged += OnModPropertyChanged;
            Mods.Add(mod);
        }
        OnPropertyChanged(nameof(ModCount));
        IsAllSelected = false;
        RefreshSelectionState();
        RefreshFilterOptions();
        ModsView.Refresh();
        ApplySort();
    }

    [RelayCommand]
    private void Add()
    {
        var dialog = new OpenFileDialog { Filter = "Minecraft Mod (*.jar)|*.jar", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var addedCount = 0;
            var duplicateNames = new List<string>();
            var failedCount = 0;
            foreach (var file in dialog.FileNames)
            {
                try
                {
                    _library.Add(file);
                    addedCount++;
                }
                catch (DuplicateLocalModException ex)
                {
                    duplicateNames.Add(ex.ExistingMod.Name);
                }
                catch
                {
                    failedCount++;
                }
            }

            LoadData();
            ShowMessage(addedCount, duplicateNames.Count, failedCount);
        }
        catch (Exception ex)
        {
            MessageText = $"添加失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void Rename(LocalMod mod)
    {
        var dialog = new Views.InputDialog("重命名 Mod", "Mod 名称：", mod.Name);
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputText)) return;
        mod.Name = dialog.InputText.Trim();
        _library.Save();
        ModsView.Refresh();
        RefreshFilterOptions();
    }

    [RelayCommand]
    private void BatchDelete()
    {
        var selected = SelectedMods;
        if (selected.Count == 0) return;
        if (!IsBatchDeletePending)
        {
            CancelPendingDelete();
            IsBatchDeletePending = true;
            return;
        }

        var deleted = _library.DeleteMany(selected);
        IsBatchDeletePending = false;
        LoadData();
        MessageText = deleted.Count == selected.Count
            ? $"已删除 {deleted.Count} 个 Mod。"
            : $"已删除 {deleted.Count} 个 Mod，{selected.Count - deleted.Count} 个删除失败，请关闭正在使用 Mod 文件的程序后重试。";
    }

    public void CancelPendingDelete()
    {
        IsBatchDeletePending = false;
        foreach (var mod in Mods.Where(mod => mod.IsDeletePending)) mod.IsDeletePending = false;
    }

    [RelayCommand]
    private void Delete(LocalMod mod)
    {
        if (!mod.IsDeletePending)
        {
            IsBatchDeletePending = false;
            foreach (var item in Mods.Where(item => item != mod && item.IsDeletePending)) item.IsDeletePending = false;
            mod.IsDeletePending = true;
            return;
        }

        _library.Delete(mod);
        Mods.Remove(mod);
        OnPropertyChanged(nameof(ModCount));
        RefreshFilterOptions();
        ModsView.Refresh();
        RefreshSelectionState();
    }

}

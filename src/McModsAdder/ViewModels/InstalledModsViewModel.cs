using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MCModPlus.Models;
using MCModPlus.Services;

namespace MCModPlus.ViewModels;

public partial class InstalledModItem : ObservableObject
{
    public InstalledMod Mod { get; }

    public string DisplayName => Mod.DisplayName;
    public string DisplayVersion => Mod.DisplayVersion;
    public string FileName => Mod.FileName;
    public string? ModId => Mod.ModId ?? Mod.ProjectSlug;

    [ObservableProperty]
    private bool _isHighlighted;

    [ObservableProperty]
    private BitmapImage? _icon;

    public InstalledModItem(InstalledMod mod)
    {
        Mod = mod;
        _ = LoadIconAsync();
    }

    private async Task LoadIconAsync()
    {
        Icon = await ImageLoader.GetAsync(Mod.IconUrl);
    }
}

public partial class InstalledModsViewModel : ObservableObject
{
    public ObservableCollection<InstalledModItem> Mods { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _searchHint = string.Empty;

    public string CountText => $"共计 {Mods.Count} 个 mod";

    public InstalledModsViewModel(IEnumerable<InstalledMod> mods)
    {
        foreach (var mod in mods)
        {
            Mods.Add(new InstalledModItem(mod));
        }
    }

    partial void OnSearchTextChanged(string value) => UpdateMatches(value);

    private void UpdateMatches(string text)
    {
        var query = text.Trim();
        foreach (var item in Mods)
        {
            item.IsHighlighted = !string.IsNullOrWhiteSpace(query)
                && (item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (item.ModId?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                    || item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = Mods.OrderByDescending(item => item.IsHighlighted)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var currentIndex = Mods.IndexOf(ordered[index]);
            if (currentIndex != index)
            {
                Mods.Move(currentIndex, index);
            }
        }

        SearchHint = !string.IsNullOrWhiteSpace(query) && Mods.All(item => !item.IsHighlighted)
            ? "没有找到匹配的 mod"
            : string.Empty;
    }
}

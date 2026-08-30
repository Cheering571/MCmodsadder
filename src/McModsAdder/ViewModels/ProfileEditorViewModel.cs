using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McModsAdder.Models;
using McModsAdder.Services;

namespace McModsAdder.ViewModels;

/// <summary>搜索结果行（带图标与"已添加"状态）</summary>
public partial class SearchResultItem : ObservableObject
{
    public ModSearchResult Result { get; }

    [ObservableProperty]
    private BitmapImage? _icon;

    [ObservableProperty]
    private bool _isAdded;

    public SearchResultItem(ModSearchResult result)
    {
        Result = result;
        _ = LoadIconAsync();
    }

    private async Task LoadIconAsync()
    {
        Icon = await ImageLoader.GetAsync(Result.IconUrl);
    }
}

public partial class ProfileEntryItem : ObservableObject
{
    public ProfileEntry Entry { get; }

    [ObservableProperty]
    private bool _isHighlighted;

    [ObservableProperty]
    private BitmapImage? _icon;

    public ProfileEntryItem(ProfileEntry entry)
    {
        Entry = entry;
        _ = LoadIconAsync();
    }

    private async Task LoadIconAsync()
    {
        Icon = await ImageLoader.GetAsync(Entry.IconUrl);
    }
}

public partial class ProfileEditorViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly ProfileService _profileService;
    private readonly IModProvider _provider;
    private readonly NavigationService _nav;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private ModProfile? _profile;

    [ObservableProperty]
    private ObservableCollection<ProfileEntry> _entries = new();

    public ObservableCollection<ProfileEntryItem> EntryItems { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _searchResults = new();

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchHint = string.Empty;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private int _pageSize = 6;

    public string PageStatus => TotalPages == 0 ? "" : $"第 {CurrentPage} / {TotalPages} 页";

    public bool CanPreviousPage => CurrentPage > 1;

    public bool CanNextPage => CurrentPage < TotalPages;

    public ProfileEditorViewModel(AppState appState, ProfileService profileService, IModProvider provider, NavigationService nav)
    {
        _appState = appState;
        _profileService = profileService;
        _provider = provider;
        _nav = nav;
    }

    public void UpdatePageSize(double availableHeight)
    {
        var newPageSize = Math.Max(1, (int)Math.Floor((availableHeight + 2) / 62));
        if (newPageSize == PageSize)
        {
            return;
        }

        PageSize = newPageSize;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            _ = SearchCurrentPageAsync();
        }
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageStatus));
        OnPropertyChanged(nameof(CanPreviousPage));
        OnPropertyChanged(nameof(CanNextPage));
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(PageStatus));
        OnPropertyChanged(nameof(CanPreviousPage));
        OnPropertyChanged(nameof(CanNextPage));
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanPreviousPage)
        {
            CurrentPage--;
            _ = SearchCurrentPageAsync();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CanNextPage)
        {
            CurrentPage++;
            _ = SearchCurrentPageAsync();
        }
    }

    private async Task SearchCurrentPageAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;
        IsSearching = true;
        try
        {
            var page = await _provider.SearchAsync(SearchText.Trim(), PageSize, (CurrentPage - 1) * PageSize, ct);
            var existingIds = Entries.Select(e => e.ProjectId).ToHashSet();
            SearchResults = new ObservableCollection<SearchResultItem>(
                page.Results.Select(r => new SearchResultItem(r) { IsAdded = existingIds.Contains(r.ProjectId) }));
            TotalPages = page.TotalHits == 0 ? 0 : (int)Math.Ceiling(page.TotalHits / (double)PageSize);
            if (TotalPages > 0 && CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }
            SearchHint = page.Results.Count == 0 ? "没有找到相关 mod" : string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SearchHint = $"搜索失败：{ex.Message}（可在设置中切换镜像源）";
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsSearching = false;
            }
        }
    }

    [RelayCommand]
    private void Back()
    {
        _nav.Navigate<Views.ProfilesPage>();
    }

    public void LoadData()
    {
        Profile = _appState.CurrentProfile;
        Entries = Profile != null
            ? new ObservableCollection<ProfileEntry>(Profile.Entries)
            : new ObservableCollection<ProfileEntry>();
        EntryItems.Clear();
        foreach (var entry in Entries)
        {
            EntryItems.Add(new ProfileEntryItem(entry));
        }
        UpdateEntryHighlights(SearchText);
    }

    private void UpdateEntryHighlights(string text)
    {
        var query = text.Trim();
        foreach (var item in EntryItems)
        {
            item.IsHighlighted = !string.IsNullOrWhiteSpace(query)
                && (item.Entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Entry.Author.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Entry.Slug.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var orderedItems = EntryItems
            .OrderByDescending(item => item.IsHighlighted)
            .ThenBy(item => item.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var targetIndex = 0; targetIndex < orderedItems.Count; targetIndex++)
        {
            var currentIndex = EntryItems.IndexOf(orderedItems[targetIndex]);
            if (currentIndex != targetIndex)
            {
                EntryItems.Move(currentIndex, targetIndex);
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateEntryHighlights(value);
        _searchCts?.Cancel();
        if (string.IsNullOrWhiteSpace(value))
        {
            CurrentPage = 1;
            TotalPages = 0;
            SearchResults = new ObservableCollection<SearchResultItem>();
            SearchHint = string.Empty;
        }
    }

    [RelayCommand]
    private void Search()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        CurrentPage = 1;
        SearchHint = string.Empty;
        _ = SearchCurrentPageAsync();
    }

    [RelayCommand]
    private void AddEntry(SearchResultItem item)
    {
        if (Profile == null || item.IsAdded)
        {
            return;
        }

        var entry = new ProfileEntry
        {
            ProjectId = item.Result.ProjectId,
            Slug = item.Result.Slug,
            Name = item.Result.Name,
            IconUrl = item.Result.IconUrl,
            Summary = item.Result.Summary,
            Author = item.Result.Author,
            Downloads = item.Result.Downloads
        };
        Profile.Entries.Add(entry);
        Entries.Add(entry);
        EntryItems.Add(new ProfileEntryItem(entry));
        UpdateEntryHighlights(SearchText);
        item.IsAdded = true;
        _profileService.Save(Profile);
    }

    [RelayCommand]
    private void RemoveEntry(ProfileEntry entry)
    {
        if (Profile == null)
        {
            return;
        }

        Profile.Entries.RemoveAll(e => e.ProjectId == entry.ProjectId);
        Entries.Remove(entry);
        var entryItem = EntryItems.FirstOrDefault(e => e.Entry.ProjectId == entry.ProjectId);
        if (entryItem != null)
        {
            EntryItems.Remove(entryItem);
        }
        var searchItem = SearchResults.FirstOrDefault(r => r.Result.ProjectId == entry.ProjectId);
        if (searchItem != null)
        {
            searchItem.IsAdded = false;
        }
        _profileService.Save(Profile);
    }
}

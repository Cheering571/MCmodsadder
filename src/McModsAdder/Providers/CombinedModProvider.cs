using MCModPlus.Models;
using MCModPlus.Services;

namespace MCModPlus.Providers;

/// <summary>合并 Modrinth 与 CurseForge 搜索，并按项目 ID 前缀路由后续操作。</summary>
public sealed class CombinedModProvider : IModProvider
{
    private readonly ModrinthProvider _modrinth;
    private readonly CurseForgeProvider _curseForge;

    public CombinedModProvider(ModrinthProvider modrinth, CurseForgeProvider curseForge)
    {
        _modrinth = modrinth;
        _curseForge = curseForge;
    }

    public string Name => "Modrinth + CurseForge";

    public async Task<ModSearchPage> SearchAsync(string query, int limit = 20, int offset = 0, CancellationToken ct = default, ModSearchSource source = ModSearchSource.All)
    {
        if (source == ModSearchSource.Modrinth)
            return await _modrinth.SearchAsync(query, limit, offset, ct, source);
        if (source == ModSearchSource.CurseForge)
            return _curseForge.IsConfigured
                ? await _curseForge.SearchAsync(query, limit, offset, ct, source)
                : new ModSearchPage();

        var pages = await Task.WhenAll(
            _modrinth.SearchAsync(query, limit, offset, ct, source),
            _curseForge.IsConfigured ? _curseForge.SearchAsync(query, limit, offset, ct, source) : Task.FromResult(new ModSearchPage()));
        var results = new List<ModSearchResult>();
        for (var index = 0; results.Count < limit; index++)
        {
            var added = false;
            foreach (var page in pages)
            {
                if (index < page.Results.Count)
                {
                    results.Add(page.Results[index]);
                    added = true;
                    if (results.Count >= limit) break;
                }
            }

            if (!added) break;
        }

        return new ModSearchPage
        {
            TotalHits = pages.Sum(p => p.TotalHits),
            Results = results
        };
    }

    public Task<ModVersionInfo?> GetBestVersionAsync(string projectIdOrSlug, string gameVersion, ModLoader loader, CancellationToken ct = default)
        => CurseForgeProvider.IsCurseForgeId(projectIdOrSlug)
            ? _curseForge.GetBestVersionAsync(projectIdOrSlug, gameVersion, loader, ct)
            : _modrinth.GetBestVersionAsync(projectIdOrSlug, gameVersion, loader, ct);

    public async Task<IReadOnlyDictionary<string, ModVersionInfo>> MatchHashesAsync(IReadOnlyCollection<string> sha1Hashes, CancellationToken ct = default)
        => await _modrinth.MatchHashesAsync(sha1Hashes, ct);

    public Task<IReadOnlyDictionary<string, string>> GetProjectNamesAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
        => MergeProjectDataAsync(projectIds, (provider, ids, token) => provider.GetProjectNamesAsync(ids, token));

    public Task<IReadOnlyDictionary<string, string>> GetProjectIconsAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
        => MergeProjectDataAsync(projectIds, (provider, ids, token) => provider.GetProjectIconsAsync(ids, token));

    public Task<IReadOnlyDictionary<string, ModProjectInfo>> GetProjectInfosAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
        => MergeProjectDataAsync(projectIds, (provider, ids, token) => provider.GetProjectInfosAsync(ids, token));

    public Task DownloadAsync(ModVersionInfo file, string destPath, IProgress<double>? progress = null, CancellationToken ct = default)
        => CurseForgeProvider.IsCurseForgeId(file.ProjectId)
            ? _curseForge.DownloadAsync(file, destPath, progress, ct)
            : _modrinth.DownloadAsync(file, destPath, progress, ct);

    private async Task<IReadOnlyDictionary<string, T>> MergeProjectDataAsync<T>(
        IReadOnlyCollection<string> ids,
        Func<IModProvider, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, T>>> fetch)
    {
        var modrinthIds = ids.Where(id => !CurseForgeProvider.IsCurseForgeId(id)).ToList();
        var curseForgeIds = ids.Where(CurseForgeProvider.IsCurseForgeId).ToList();
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in await fetch(_modrinth, modrinthIds, CancellationToken.None)) result[pair.Key] = pair.Value;
        if (_curseForge.IsConfigured)
            foreach (var pair in await fetch(_curseForge, curseForgeIds, CancellationToken.None)) result[pair.Key] = pair.Value;
        return result;
    }
}

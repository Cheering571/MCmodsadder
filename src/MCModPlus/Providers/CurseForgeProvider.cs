using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MCModPlus.Models;
using MCModPlus.Services;

namespace MCModPlus.Providers;

/// <summary>CurseForge Mod API 实现。API Key 由用户在设置中配置。</summary>
public sealed class CurseForgeProvider : IModProvider
{
    private const string BaseUrl = "https://api.curseforge.com/v1/";
    private const int MinecraftGameId = 432;
    private const int ModClassId = 6;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;
    private readonly HttpClient _downloadHttp;
    private string _apiKey = string.Empty;

    public CurseForgeProvider(HttpClient http, SettingsService settings)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(60);
        _downloadHttp = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            MaxConnectionsPerServer = 8
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MCModPlus/1.0");
        }
        SetApiKey(string.IsNullOrWhiteSpace(settings.Current.CurseForgeApiKey)
            ? AppSettings.GetDefaultCurseForgeApiKey()
            : settings.Current.CurseForgeApiKey);
    }

    public string Name => "CurseForge";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public void SetApiKey(string? apiKey)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey)
            ? AppSettings.GetDefaultCurseForgeApiKey()
            : apiKey.Trim();
        _http.DefaultRequestHeaders.Remove("x-api-key");
        if (IsConfigured)
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _apiKey);
        }
    }

    public async Task<ModSearchPage> SearchAsync(string query, int limit = 20, int offset = 0, CancellationToken ct = default, ModSearchSource source = ModSearchSource.All)
    {
        EnsureConfigured();
        var normalizedQuery = NormalizeSearchText(query);
        var url = $"mods/search?gameId={MinecraftGameId}&classId={ModClassId}&searchFilter={Uri.EscapeDataString(query.Trim())}&sortField=2&sortOrder=desc&pageSize=50&index=0";
        var response = await GetAsync<ListResponse<CurseMod>>(url, ct);
        var allResults = (response?.Data ?? new List<CurseMod>())
            .Select(ToSearchResult)
            .Where(result => IsSearchMatch(result, normalizedQuery))
            .OrderByDescending(result => GetSearchRelevance(result, normalizedQuery))
            .ThenByDescending(result => result.Downloads)
            .ToList();
        return new ModSearchPage
        {
            TotalHits = allResults.Count,
            Results = allResults.Skip(Math.Max(0, offset)).Take(Math.Max(1, limit)).ToList()
        };
    }

    public async Task<ModVersionInfo?> GetBestVersionAsync(string projectIdOrSlug, string gameVersion, ModLoader loader, CancellationToken ct = default)
    {
        EnsureConfigured();
        var id = Unprefix(projectIdOrSlug);
        var url = $"mods/{Uri.EscapeDataString(id)}/files?gameVersion={Uri.EscapeDataString(gameVersion)}&pageSize=50";
        if (TryGetLoaderType(loader, out var loaderType)) url += $"&modLoaderType={loaderType}";
        var response = await GetAsync<ListResponse<CurseFile>>(url, ct);
        var file = (response?.Data ?? new List<CurseFile>()).Where(f => f.IsAvailable && !f.IsServerPack && !string.IsNullOrWhiteSpace(f.FileName))
            .OrderByDescending(f => f.ReleaseType == 1).ThenByDescending(f => f.FileDate).FirstOrDefault();
        if (file == null) return null;
        var downloadUrl = file.DownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            var urlResponse = await GetAsync<Response<string>>($"mods/{id}/files/{file.Id}/download-url", ct);
            downloadUrl = urlResponse?.Data;
        }
        if (string.IsNullOrWhiteSpace(downloadUrl)) return null;
        return ToVersionInfo(file, downloadUrl);
    }

    public Task<IReadOnlyDictionary<string, ModVersionInfo>> MatchHashesAsync(IReadOnlyCollection<string> sha1Hashes, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, ModVersionInfo>>(new Dictionary<string, ModVersionInfo>());

    public async Task<IReadOnlyDictionary<string, string>> GetProjectNamesAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
    {
        EnsureConfigured();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in projectIds.Where(IsCurseForgeId).Select(Unprefix).Distinct())
        {
            var response = await GetAsync<Response<CurseMod>>($"mods/{Uri.EscapeDataString(id)}", ct);
            if (response?.Data != null) result[Prefix(id)] = response.Data.Name;
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetProjectIconsAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
    {
        EnsureConfigured();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in projectIds.Where(IsCurseForgeId).Select(Unprefix).Distinct())
        {
            var response = await GetAsync<Response<CurseMod>>($"mods/{Uri.EscapeDataString(id)}", ct);
            if (!string.IsNullOrWhiteSpace(response?.Data?.Logo?.Url)) result[Prefix(id)] = response.Data.Logo.Url;
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<string, ModProjectInfo>> GetProjectInfosAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
    {
        EnsureConfigured();
        var result = new Dictionary<string, ModProjectInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in projectIds.Where(IsCurseForgeId).Select(Unprefix).Distinct())
        {
            var response = await GetAsync<Response<CurseMod>>($"mods/{Uri.EscapeDataString(id)}", ct);
            if (response?.Data != null) result[Prefix(id)] = new ModProjectInfo { Name = response.Data.Name, IconUrl = response.Data.Logo?.Url ?? string.Empty };
        }
        return result;
    }

    public async Task DownloadAsync(ModVersionInfo file, string destPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnsureConfigured();
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var tmpPath = destPath + $".{Guid.NewGuid():N}.download";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, file.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("MCModPlus/1.0");
            request.Headers.Referrer = new Uri("https://www.curseforge.com/");
            using var response = await _downloadHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"CurseForge 下载失败（HTTP {(int)response.StatusCode} {response.ReasonPhrase}）：{detail[..Math.Min(detail.Length, 200)]}");
            }
            {
                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var total = response.Content.Headers.ContentLength ?? file.Size;
                var received = 0L;
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    received += read;
                    if (total > 0) progress?.Report((double)received / total);
                }
            }
            if (!string.IsNullOrWhiteSpace(file.Sha1) && !string.Equals(await ComputeSha1Async(tmpPath, ct), file.Sha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"SHA1 校验失败: {file.FileName}");
            await MoveWithRetryAsync(tmpPath, destPath, ct);
        }
        finally { if (File.Exists(tmpPath)) try { File.Delete(tmpPath); } catch { } }
    }

    private static async Task MoveWithRetryAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        IOException? lastException = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException ex) when (attempt < 7)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), ct);
            }
        }

        throw lastException!;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException("CurseForge 未配置 API Key，请先在设置页面填写 CurseForge API Key。");
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"CurseForge API 请求失败（HTTP {(int)response.StatusCode} {response.ReasonPhrase}）：{detail[..Math.Min(detail.Length, 300)]}");
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
    }

    private static bool IsSearchMatch(ModSearchResult result, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var name = NormalizeSearchText(result.Name);
        var slug = NormalizeSearchText(result.Slug);
        var compactQuery = CompactSearchText(query);
        return name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || slug.Contains(query, StringComparison.OrdinalIgnoreCase)
            || CompactSearchText(name).Contains(compactQuery, StringComparison.OrdinalIgnoreCase)
            || CompactSearchText(slug).Contains(compactQuery, StringComparison.OrdinalIgnoreCase)
            || GetSearchTerms(query).Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase) || slug.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetSearchRelevance(ModSearchResult result, string query)
    {
        var name = NormalizeSearchText(result.Name);
        var slug = NormalizeSearchText(result.Slug);
        var compactName = CompactSearchText(name);
        var compactSlug = CompactSearchText(slug);
        var compactQuery = CompactSearchText(query);
        var terms = GetSearchTerms(query);
        var matchedTerms = terms.Count(term => name.Contains(term, StringComparison.OrdinalIgnoreCase) || slug.Contains(term, StringComparison.OrdinalIgnoreCase));
        var score = matchedTerms * 100;
        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(slug, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(compactName, compactQuery, StringComparison.OrdinalIgnoreCase)
            || string.Equals(compactSlug, compactQuery, StringComparison.OrdinalIgnoreCase)) score += 1000;
        else if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase) || slug.StartsWith(query, StringComparison.OrdinalIgnoreCase)
            || compactName.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase) || compactSlug.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase)) score += 500;
        else if (name.Contains(query, StringComparison.OrdinalIgnoreCase) || slug.Contains(query, StringComparison.OrdinalIgnoreCase)
            || compactName.Contains(compactQuery, StringComparison.OrdinalIgnoreCase) || compactSlug.Contains(compactQuery, StringComparison.OrdinalIgnoreCase)) score += 250;
        return score;
    }

    private static string[] GetSearchTerms(string value)
        => NormalizeSearchText(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CompactSearchText(string value)
        => string.Concat(value.Where(char.IsLetterOrDigit));

    private static string NormalizeSearchText(string value)
        => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static ModSearchResult ToSearchResult(CurseMod mod) => new()
    {
        ProjectId = Prefix(mod.Id.ToString()), Slug = mod.Slug, Name = mod.Name, Summary = mod.Summary ?? string.Empty,
        Author = mod.Authors?.FirstOrDefault()?.Name ?? string.Empty, IconUrl = mod.Logo?.Url ?? string.Empty,
        Downloads = mod.DownloadCount, Source = "CurseForge", SourceUrl = mod.Links?.WebsiteUrl ?? $"https://www.curseforge.com/minecraft/mc-mods/{mod.Slug}",
        McModUrl = $"https://search.mcmod.cn/s?key={Uri.EscapeDataString(mod.Name)}"
    };

    private static ModVersionInfo ToVersionInfo(CurseFile file, string downloadUrl) => new()
    {
        VersionId = Prefix(file.Id.ToString()), ProjectId = Prefix(file.ModId.ToString()), VersionNumber = file.DisplayName,
        Name = file.DisplayName, DownloadUrl = downloadUrl, FileName = file.FileName, Sha1 = file.Hashes?.FirstOrDefault(h => string.Equals(h.Algorithm, "sha1", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
        Size = file.FileLength, Dependencies = file.Dependencies?.Where(d => d.RelationType == 3).Select(d => new ModDependencyInfo { ProjectId = Prefix(d.ModId.ToString()), Required = true }).ToList() ?? new()
    };

    private static bool TryGetLoaderType(ModLoader loader, out int type)
    {
        type = loader switch { ModLoader.Forge => 1, ModLoader.Fabric => 4, ModLoader.Quilt => 5, ModLoader.NeoForge => 6, _ => 0 };
        return type != 0;
    }
    public static bool IsCurseForgeId(string id) => id.StartsWith("cf:", StringComparison.OrdinalIgnoreCase);
    public static string Prefix(string id) => $"cf:{id}";
    public static string Unprefix(string id) => id.StartsWith("cf:", StringComparison.OrdinalIgnoreCase) ? id[3..] : id;
    private static async Task<string> ComputeSha1Async(string path, CancellationToken ct) { await using var stream = File.OpenRead(path); using var sha1 = SHA1.Create(); return Convert.ToHexString(await sha1.ComputeHashAsync(stream, ct)).ToLowerInvariant(); }

    private sealed class ListResponse<T> { public List<T>? Data { get; set; } public Pagination? Pagination { get; set; } }
    private sealed class Response<T> { public T? Data { get; set; } public Pagination? Pagination { get; set; } }
    private sealed class Pagination { public int TotalCount { get; set; } }
    private sealed class CurseMod { public int Id { get; set; } public string Name { get; set; } = ""; public string Slug { get; set; } = ""; public string? Summary { get; set; } public long DownloadCount { get; set; } public CurseLinks? Links { get; set; } public CurseLogo? Logo { get; set; } public List<CurseAuthor>? Authors { get; set; } }
    private sealed class CurseFile { public int Id { get; set; } public int ModId { get; set; } public bool IsAvailable { get; set; } = true; public string FileName { get; set; } = ""; public string DisplayName { get; set; } = ""; public int ReleaseType { get; set; } public DateTime FileDate { get; set; } public long FileLength { get; set; } public string? DownloadUrl { get; set; } public bool IsServerPack { get; set; } public List<CurseHash>? Hashes { get; set; } public List<CurseDependency>? Dependencies { get; set; } }
    private sealed class CurseDependency { public int ModId { get; set; } public int RelationType { get; set; } }
    private sealed class CurseHash { public string Algorithm { get; set; } = ""; public string Value { get; set; } = ""; }
    private sealed class CurseLinks { public string? WebsiteUrl { get; set; } }
    private sealed class CurseLogo { public string? Url { get; set; } }
    private sealed class CurseAuthor { public string Name { get; set; } = ""; }
}

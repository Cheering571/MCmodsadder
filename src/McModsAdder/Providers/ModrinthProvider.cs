using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MCModPlus.Models;
using MCModPlus.Models.Dto;
using MCModPlus.Services;

namespace MCModPlus.Providers;

/// <summary>
/// Modrinth API 实现。支持官方源与 MCIM 国内镜像切换。
/// </summary>
public class ModrinthProvider : IModProvider
{
    public const string OfficialBase = "https://api.modrinth.com/v2/";
    public const string MirrorBase = "https://mod.mcimirror.top/modrinth/v2/";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly SettingsService _settings;

    public ModrinthProvider(HttpClient http, SettingsService settings)
    {
        _http = http;
        _settings = settings;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MCModPlus/1.0 (github.com/MCModPlus)");
        }
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public string Name => "Modrinth";

    private string ApiBase => _settings.Current.UseMirror ? MirrorBase : OfficialBase;

    // ---------------- 搜索 ----------------

    public async Task<ModSearchPage> SearchAsync(string query, int limit = 20, int offset = 0, CancellationToken ct = default, ModSearchSource source = ModSearchSource.All)
    {
        var facets = Uri.EscapeDataString("[[\"project_type:mod\"]]");
        var url = $"{ApiBase}search?query={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}&facets={facets}";
        var resp = await GetWithRetryAsync<MrSearchResponse>(url, ct);
        return new ModSearchPage
        {
            TotalHits = resp?.TotalHits ?? 0,
            Results = resp?.Hits.Select(h => new ModSearchResult
            {
                ProjectId = h.ProjectId,
                Slug = h.Slug,
                Name = h.Title,
                Summary = h.Description,
                Author = h.Author,
                IconUrl = h.IconUrl ?? string.Empty,
                Downloads = h.Downloads,
                Source = "Modrinth",
                SourceUrl = $"https://modrinth.com/mod/{Uri.EscapeDataString(h.Slug)}",
                McModUrl = $"https://search.mcmod.cn/s?key={Uri.EscapeDataString(h.Title)}"
            }).ToList() ?? new List<ModSearchResult>()
        };
    }

    // ---------------- 版本匹配 ----------------

    public async Task<ModVersionInfo?> GetBestVersionAsync(string projectIdOrSlug, string gameVersion, ModLoader loader, CancellationToken ct = default)
    {
        var loadersParam = BuildLoadersParam(loader, out var preferFallback);
        var url = $"{ApiBase}project/{Uri.EscapeDataString(projectIdOrSlug)}/version" +
                  $"?game_versions={Uri.EscapeDataString($"[\"{gameVersion}\"]")}" +
                  $"&loaders={Uri.EscapeDataString(loadersParam)}";

        var versions = await GetWithRetryAsync<List<MrVersion>>(url, ct);
        if (versions == null || versions.Count == 0)
        {
            return null;
        }

        IEnumerable<MrVersion> candidates = versions;

        // Quilt 实例：优先 quilt 原生版本，再回退 fabric 版本
        if (loader == ModLoader.Quilt)
        {
            var quiltNative = candidates.Where(v => v.Loaders.Contains("quilt")).ToList();
            if (quiltNative.Count > 0)
            {
                candidates = quiltNative;
            }
        }

        // 优先 release，列表本身按发布时间倒序
        var best = candidates.FirstOrDefault(v => v.VersionType == "release") ?? candidates.First();
        return ToVersionInfo(best);
    }

    private static string BuildLoadersParam(ModLoader loader, out bool preferFallback)
    {
        preferFallback = false;
        return loader switch
        {
            ModLoader.Quilt => "[\"quilt\",\"fabric\"]",
            ModLoader.Unknown => "[\"fabric\",\"forge\",\"quilt\",\"neoforge\"]",
            _ => $"[\"{loader.ToApiName()}\"]"
        };
    }

    private static ModVersionInfo? ToVersionInfo(MrVersion v)
    {
        var file = v.Files.FirstOrDefault(f => f.Primary) ?? v.Files.FirstOrDefault();
        if (file == null || string.IsNullOrEmpty(file.Url))
        {
            return null;
        }

        return new ModVersionInfo
        {
            VersionId = v.Id,
            ProjectId = v.ProjectId,
            VersionNumber = v.VersionNumber,
            Name = v.Name,
            DownloadUrl = file.Url,
            FileName = file.Filename,
            Sha1 = file.Hashes.Sha1,
            Size = file.Size,
            Dependencies = v.Dependencies
                .Where(d => d.ProjectId != null)
                .Select(d => new ModDependencyInfo
                {
                    ProjectId = d.ProjectId,
                    VersionId = d.VersionId,
                    Required = d.DependencyType == "required"
                })
                .ToList()
        };
    }

    // ---------------- 哈希匹配 ----------------

    public async Task<IReadOnlyDictionary<string, ModVersionInfo>> MatchHashesAsync(IReadOnlyCollection<string> sha1Hashes, CancellationToken ct = default)
    {
        var result = new Dictionary<string, ModVersionInfo>();
        if (sha1Hashes.Count == 0)
        {
            return result;
        }

        // Modrinth 对单次请求数量有限制，分批提交
        foreach (var batch in sha1Hashes.Distinct().Chunk(200))
        {
            ct.ThrowIfCancellationRequested();
            var req = new MrVersionFilesRequest { Hashes = batch.ToList() };
            using var resp = await _http.PostAsJsonAsync($"{ApiBase}version_files", req, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();
            var map = await resp.Content.ReadFromJsonAsync<Dictionary<string, MrVersion>>(JsonOpts, ct);
            if (map == null)
            {
                continue;
            }

            foreach (var (hash, version) in map)
            {
                var info = ToVersionInfo(version);
                if (info != null)
                {
                    result[hash] = info;
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetProjectNamesAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        if (projectIds.Count == 0)
        {
            return result;
        }

        var ids = string.Join(",", projectIds.Distinct().Select(id => $"\"{id}\""));
        var url = $"{ApiBase}projects?ids={Uri.EscapeDataString($"[{ids}]")}";
        var projects = await GetWithRetryAsync<List<MrProject>>(url, ct);
        if (projects != null)
        {
            foreach (var p in projects)
            {
                result[p.Id] = p.Title;
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetProjectIconsAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        if (projectIds.Count == 0)
        {
            return result;
        }

        var ids = string.Join(",", projectIds.Distinct().Select(id => $"\"{id}\""));
        var url = $"{ApiBase}projects?ids={Uri.EscapeDataString($"[{ids}]")}";
        var projects = await GetWithRetryAsync<List<MrProject>>(url, ct);
        if (projects != null)
        {
            foreach (var project in projects)
            {
                if (!string.IsNullOrWhiteSpace(project.IconUrl))
                {
                    result[project.Id] = project.IconUrl;
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, ModProjectInfo>> GetProjectInfosAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default)
    {
        var result = new Dictionary<string, ModProjectInfo>();
        if (projectIds.Count == 0)
        {
            return result;
        }

        var ids = string.Join(",", projectIds.Distinct().Select(id => $"\"{id}\""));
        var url = $"{ApiBase}projects?ids={Uri.EscapeDataString($"[{ids}]")}";
        var projects = await GetWithRetryAsync<List<MrProject>>(url, ct);
        if (projects != null)
        {
            foreach (var project in projects)
            {
                result[project.Id] = new ModProjectInfo
                {
                    Name = project.Title,
                    IconUrl = project.IconUrl ?? string.Empty
                };
            }
        }

        return result;
    }

    // ---------------- 下载 ----------------

    public async Task DownloadAsync(ModVersionInfo file, string destPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmpPath = destPath + ".download";
        try
        {
            using (var resp = await _http.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? file.Size;
                await using var input = await resp.Content.ReadAsStreamAsync(ct);
                await using var output = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    received += read;
                    if (total > 0)
                    {
                        progress?.Report((double)received / total);
                    }
                }
            }

            // 校验 sha1
            if (!string.IsNullOrEmpty(file.Sha1))
            {
                var actual = await ComputeSha1Async(tmpPath, ct);
                if (!actual.Equals(file.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"SHA1 校验失败: {file.FileName}");
                }
            }

            File.Move(tmpPath, destPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* 忽略清理失败 */ }
            }
        }
    }

    public static async Task<string> ComputeSha1Async(string path, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha1 = SHA1.Create();
        var hash = await sha1.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ---------------- 工具 ----------------

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken ct, int maxAttempts = 3)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await _http.GetFromJsonAsync<T>(url, JsonOpts, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt < maxAttempts - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt)), ct);
                }
            }
        }

        throw new HttpRequestException($"请求失败: {url}", lastError);
    }
}

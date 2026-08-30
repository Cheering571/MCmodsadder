using McModsAdder.Models;

namespace McModsAdder.Services;

/// <summary>搜索结果条目</summary>
public class ModSearchResult
{
    public string ProjectId { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public long Downloads { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string McModUrl { get; set; } = string.Empty;
}

public class ModSearchPage
{
    public IReadOnlyList<ModSearchResult> Results { get; init; } = Array.Empty<ModSearchResult>();
    public int TotalHits { get; init; }
}

/// <summary>一个可下载的版本文件（含依赖信息）</summary>
public class ModVersionInfo
{
    public string VersionId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public long Size { get; set; }
    public List<ModDependencyInfo> Dependencies { get; set; } = new();
}

public class ModDependencyInfo
{
    public string? ProjectId { get; set; }
    public string? VersionId { get; set; }
    public bool Required { get; set; }
}

/// <summary>
/// Mod 来源平台抽象。一期实现 Modrinth，二期接入 CurseForge。
/// </summary>
public interface IModProvider
{
    string Name { get; }

    Task<ModSearchPage> SearchAsync(string query, int limit = 20, int offset = 0, CancellationToken ct = default);

    /// <summary>
    /// 按 MC 版本 + 加载器取最新匹配版本。loader 为 Quilt 时内部自动回退附加 fabric 过滤。
    /// 找不到匹配版本返回 null。
    /// </summary>
    Task<ModVersionInfo?> GetBestVersionAsync(string projectIdOrSlug, string gameVersion, ModLoader loader, CancellationToken ct = default);

    /// <summary>
    /// 批量 sha1 哈希匹配，返回 sha1 -> 命中的版本信息（未命中的 key 不出现）。
    /// </summary>
    Task<IReadOnlyDictionary<string, ModVersionInfo>> MatchHashesAsync(IReadOnlyCollection<string> sha1Hashes, CancellationToken ct = default);

    /// <summary>
    /// 批量获取项目名（用于哈希命中后的名称展示）。key 为项目 ID。
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetProjectNamesAsync(IReadOnlyCollection<string> projectIds, CancellationToken ct = default);

    /// <summary>
    /// 下载文件到指定路径并校验 sha1。失败抛异常。
    /// </summary>
    Task DownloadAsync(ModVersionInfo file, string destPath, IProgress<double>? progress = null, CancellationToken ct = default);
}

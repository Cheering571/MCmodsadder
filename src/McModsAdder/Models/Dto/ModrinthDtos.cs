using System.Text.Json.Serialization;

namespace McModsAdder.Models.Dto;

// ---------- 搜索 ----------

public class MrSearchResponse
{
    [JsonPropertyName("hits")] public List<MrSearchHit> Hits { get; set; } = new();
    [JsonPropertyName("total_hits")] public int TotalHits { get; set; }
}

public class MrSearchHit
{
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = string.Empty;
    [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("author")] public string Author { get; set; } = string.Empty;
    [JsonPropertyName("downloads")] public long Downloads { get; set; }
    [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = new();
}

// ---------- 项目 ----------

public class MrProject
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }
    [JsonPropertyName("game_versions")] public List<string> GameVersions { get; set; } = new();
    [JsonPropertyName("loaders")] public List<string> Loaders { get; set; } = new();
    [JsonPropertyName("downloads")] public long Downloads { get; set; }
}

// ---------- 版本 ----------

public class MrVersion
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("project_id")] public string ProjectId { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("version_number")] public string VersionNumber { get; set; } = string.Empty;
    [JsonPropertyName("version_type")] public string VersionType { get; set; } = "release";
    [JsonPropertyName("game_versions")] public List<string> GameVersions { get; set; } = new();
    [JsonPropertyName("loaders")] public List<string> Loaders { get; set; } = new();
    [JsonPropertyName("featured")] public bool Featured { get; set; }
    [JsonPropertyName("date_published")] public DateTime DatePublished { get; set; }
    [JsonPropertyName("files")] public List<MrFile> Files { get; set; } = new();
    [JsonPropertyName("dependencies")] public List<MrDependency> Dependencies { get; set; } = new();
}

public class MrFile
{
    [JsonPropertyName("hashes")] public MrHashes Hashes { get; set; } = new();
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("filename")] public string Filename { get; set; } = string.Empty;
    [JsonPropertyName("primary")] public bool Primary { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
}

public class MrHashes
{
    [JsonPropertyName("sha1")] public string Sha1 { get; set; } = string.Empty;
    [JsonPropertyName("sha512")] public string Sha512 { get; set; } = string.Empty;
}

public class MrDependency
{
    [JsonPropertyName("version_id")] public string? VersionId { get; set; }
    [JsonPropertyName("project_id")] public string? ProjectId { get; set; }
    [JsonPropertyName("file_name")] public string? FileName { get; set; }
    [JsonPropertyName("dependency_type")] public string DependencyType { get; set; } = "optional";
}

// ---------- 哈希批量查询 ----------

public class MrVersionFilesRequest
{
    [JsonPropertyName("hashes")] public List<string> Hashes { get; set; } = new();
    [JsonPropertyName("algorithm")] public string Algorithm { get; set; } = "sha1";
}

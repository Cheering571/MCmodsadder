using System.IO;

namespace McModsAdder.Models;

public enum ModIdentifyMethod
{
    None,
    Hash,
    Metadata
}

/// <summary>
/// mods 目录中已存在的一个 jar 文件
/// </summary>
public class InstalledMod
{
    public string FileName { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public string Sha1 { get; set; } = string.Empty;

    /// <summary>哈希匹配到的 Modrinth 项目 ID</summary>
    public string? ProjectId { get; set; }

    public string? ProjectSlug { get; set; }

    public string? ProjectName { get; set; }

    public string? MatchedVersionId { get; set; }

    public string? MatchedVersionNumber { get; set; }

    /// <summary>兜底解析出的 mod id（fabric id / forge modId）</summary>
    public string? ModId { get; set; }

    public string? MetadataName { get; set; }

    public string? MetadataVersion { get; set; }

    public ModIdentifyMethod IdentifyMethod { get; set; } = ModIdentifyMethod.None;

    public string DisplayName =>
        ProjectName
        ?? MetadataName
        ?? ModId
        ?? Path.GetFileNameWithoutExtension(FileName);

    public string DisplayVersion =>
        MatchedVersionNumber ?? MetadataVersion ?? string.Empty;
}

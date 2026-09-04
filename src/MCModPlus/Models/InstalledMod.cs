using System.IO;
using System.Text.RegularExpressions;

namespace MCModPlus.Models;

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

    public string? IconUrl { get; set; }

    public string? MatchedVersionId { get; set; }

    public string? MatchedVersionNumber { get; set; }

    /// <summary>兜底解析出的 mod id（fabric id / forge modId）</summary>
    public string? ModId { get; set; }

    public string? MetadataName { get; set; }

    public string? MetadataVersion { get; set; }

    /// <summary>从 Mod 元数据依赖或插件声明中识别的 Minecraft 版本范围。</summary>
    public string? MetadataGameVersion { get; set; }

    /// <summary>从 jar Manifest 读取的实现版本。</summary>
    public string? ManifestVersion { get; set; }

    /// <summary>从 jar Manifest 读取的 Minecraft 版本或版本范围。</summary>
    public string? ManifestGameVersion { get; set; }

    public ModIdentifyMethod IdentifyMethod { get; set; } = ModIdentifyMethod.None;

    public string DisplayName =>
        ProjectName
        ?? MetadataName
        ?? ModId
        ?? Path.GetFileNameWithoutExtension(FileName);

    public string DisplayVersion =>
        CleanVersion(MatchedVersionNumber)
        ?? CleanVersion(MetadataVersion)
        ?? CleanVersion(ManifestVersion)
        ?? "未知";

    public string DisplayGameVersion =>
        MetadataGameVersion
        ?? ManifestGameVersion
        ?? string.Empty;

    private static string? CleanVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Trim('"', '\'');
        cleaned = Regex.Replace(cleaned, @"\$\{[^}]+}", string.Empty).Trim(' ', '-', '_', '.', '(', ')');
        return string.IsNullOrWhiteSpace(cleaned) || cleaned.Contains('=') || cleaned.Contains("${", StringComparison.Ordinal)
            ? null
            : cleaned;
    }
}

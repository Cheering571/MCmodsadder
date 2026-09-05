using System.Text.Json.Serialization;

namespace MCModPlus.Models;

public class AppSettings
{
    /// <summary>是否使用 MCIM 国内镜像</summary>
    public bool UseMirror { get; set; }

    public int DownloadConcurrency { get; set; } = 4;

    public bool BackupEnabled { get; set; } = true;

    /// <summary>运行时使用的 CurseForge API Key；为空时表示未配置</summary>
    [JsonIgnore]
    public string CurseForgeApiKey { get; set; } = string.Empty;

    /// <summary>使用 Windows DPAPI 保护后的 CurseForge API Key</summary>
    public string? CurseForgeApiKeyProtected { get; set; }

    public static string GetDefaultCurseForgeApiKey() => string.Empty;

    /// <summary>是否自动安装必需依赖</summary>
    public bool AutoInstallDependencies { get; set; } = true;
}

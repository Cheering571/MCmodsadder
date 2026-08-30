namespace McModsAdder.Models;

public class AppSettings
{
    /// <summary>是否使用 MCIM 国内镜像</summary>
    public bool UseMirror { get; set; }

    public int DownloadConcurrency { get; set; } = 4;

    public bool BackupEnabled { get; set; } = true;

    /// <summary>是否自动安装必需依赖</summary>
    public bool AutoInstallDependencies { get; set; } = true;
}

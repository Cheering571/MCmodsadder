namespace McModsAdder.Models;

/// <summary>
/// 一个 Minecraft 实例（版本隔离目录，如 .minecraft/versions/xxx）
/// </summary>
public class GameInstance
{
    public string Name { get; set; } = string.Empty;

    /// <summary>实例目录完整路径（versions/&lt;名称&gt;）</summary>
    public string DirectoryPath { get; set; } = string.Empty;

    public string GameVersion { get; set; } = string.Empty;

    public ModLoader Loader { get; set; } = ModLoader.Unknown;

    public string LoaderVersion { get; set; } = string.Empty;

    /// <summary>mods 目录完整路径</summary>
    public string ModsPath { get; set; } = string.Empty;

    /// <summary>是否是模组实例（识别出加载器）</summary>
    public bool IsModded => Loader != ModLoader.Unknown;

    public List<InstalledMod> InstalledMods { get; set; } = new();
}

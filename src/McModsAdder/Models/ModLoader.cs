namespace McModsAdder.Models;

public enum ModLoader
{
    Unknown,
    Fabric,
    Forge,
    Quilt,
    NeoForge
}

public static class ModLoaderExtensions
{
    public static string ToApiName(this ModLoader loader) => loader switch
    {
        ModLoader.Fabric => "fabric",
        ModLoader.Forge => "forge",
        ModLoader.Quilt => "quilt",
        ModLoader.NeoForge => "neoforge",
        _ => string.Empty
    };

    public static string ToDisplay(this ModLoader loader) => loader switch
    {
        ModLoader.Fabric => "Fabric",
        ModLoader.Forge => "Forge",
        ModLoader.Quilt => "Quilt",
        ModLoader.NeoForge => "NeoForge",
        _ => "未知"
    };
}

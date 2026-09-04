using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace MCModPlus.Models;

public class AppSettings
{
    private const string DefaultCurseForgeApiKeyCiphertext = "6J7VfezqPgQwWjSLUf5HCbDk3HAvqTVnfOf4urfS3JeYlGumBWnl4exbC3x9GMrNkJ1WvkV8nv0uubHial/Njw==";
    private const string DefaultKeySeedPart1 = "MCModPlus|CurseForge|";
    private const string DefaultKeySeedPart2 = "DefaultKey|2026";
    private static readonly byte[] DefaultKeyIv = { 0x42, 0x4F, 0x6E, 0x47, 0x72, 0x75, 0x73, 0x65, 0x46, 0x6F, 0x72, 0x67, 0x65, 0x4B, 0x65, 0x79 };

    /// <summary>是否使用 MCIM 国内镜像</summary>
    public bool UseMirror { get; set; }

    public int DownloadConcurrency { get; set; } = 4;

    public bool BackupEnabled { get; set; } = true;

    /// <summary>运行时使用的 CurseForge API Key；为空时由 Provider 使用内置 Key</summary>
    [JsonIgnore]
    public string CurseForgeApiKey { get; set; } = string.Empty;

    /// <summary>使用 Windows DPAPI 保护后的 CurseForge API Key</summary>
    public string? CurseForgeApiKeyProtected { get; set; }

    public static string GetDefaultCurseForgeApiKey()
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(DefaultKeySeedPart1 + DefaultKeySeedPart2));
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = DefaultKeyIv;
        using var decryptor = aes.CreateDecryptor();
        var ciphertext = Convert.FromBase64String(DefaultCurseForgeApiKeyCiphertext);
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>是否自动安装必需依赖</summary>
    public bool AutoInstallDependencies { get; set; } = true;
}

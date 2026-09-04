using CommunityToolkit.Mvvm.ComponentModel;

namespace MCModPlus.Models;

/// <summary>
/// 配置表中的一个条目：只记录项目标识，不记录版本/加载器
/// </summary>
public class ProfileEntry
{
    public string ProjectId { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public long Downloads { get; set; }

    public string? LocalModId { get; set; }

    public string LocalVersion { get; set; } = "未知";

    public ModLoader LocalLoader { get; set; } = ModLoader.Unknown;

    public string LocalGameVersion { get; set; } = "未知";

    public DateTime AddedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 配置表：一组需要加入整合包的 mod 清单
/// </summary>
public partial class ModProfile : ObservableObject
{
    [ObservableProperty]
    private bool _isDeletePending;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "新建配置表";

    public List<ProfileEntry> Entries { get; set; } = new();

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

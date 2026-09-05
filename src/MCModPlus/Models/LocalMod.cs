using CommunityToolkit.Mvvm.ComponentModel;

namespace MCModPlus.Models;

/// <summary>由应用托管并可离线使用的本地 Mod 文件。</summary>
public partial class LocalMod : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public string Version { get; set; } = "未知";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDeletePending;

    [ObservableProperty]
    private ModLoader _loader = ModLoader.Unknown;

    [ObservableProperty]
    private string _gameVersion = "未知";

    public string GameVersionSortKey
    {
        get
        {
            var parts = GameVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return string.Join('.', parts.Select(part => int.TryParse(part, out var number) ? number.ToString("D5") : part));
        }
    }

    public DateTime AddedAt { get; set; } = DateTime.Now;
}

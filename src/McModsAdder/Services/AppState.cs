using McModsAdder.Models;

namespace McModsAdder.Services;

/// <summary>
/// 跨页面共享状态（当前选中实例、当前编辑配置表等）。
/// </summary>
public class AppState
{
    public GameInstance? CurrentInstance { get; set; }

    public ModProfile? CurrentProfile { get; set; }

    /// <summary>实例详情页最近一次对比结果（供安装页使用）</summary>
    public List<ComparisonRow>? LastComparison { get; set; }

    public List<InstallItem>? LastPlan { get; set; }

    public List<ProfileEntry>? LastUnavailable { get; set; }
}

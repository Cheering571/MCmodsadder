using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;
using MCModPlus.Services;
using MCModPlus.Views;

namespace MCModPlus.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly InstanceScanner _scanner;
    private readonly SettingsService _settings;
    private readonly ProfileService _profiles;
    private readonly LocalModLibraryService _library;
    private readonly NavigationService _nav;

    [ObservableProperty]
    private int _instanceCount;

    [ObservableProperty]
    private int _modCount;

    [ObservableProperty]
    private int _profileCount;

    [ObservableProperty]
    private string _scanStatus = "等待扫描世界";

    [ObservableProperty]
    private string _suggestionText = "没识别到你想要的实例目录？试试手动添加吧！";

    private static readonly string[] Suggestions =
    {
        "没识别到你想要的实例目录？试试手动添加吧！",
        "创建配置表，让自己想要的 Mod 可以快捷添加。",
        "给本地的 Mod 加入本地库，这样就可以加入到配置表里哦。",
        "添加 Mod 前不用确认 Minecraft 版本与加载器版本。",
        "使用实例管理查看不同启动器中的游戏目录。",
        "把常用的性能优化 Mod 整理成一份配置表，打造流畅的冒险体验。",
        "探索 Modrinth 与 CurseForge，寻找与你自己喜欢的 Mod。",
        "为大型整合包单独建立配置表，方便记录和复用专属 Mod 组合。",
        "定期检查本地 Mod 库中的文件，让收藏始终保持可用。",
        "为多人服务器准备专属 Mod 清单，避免把单人内容带入联机环境。",
        "将探索、科技、魔法等不同主题的 Mod 分组，组合出你的专属玩法。",
        "发现喜欢的 Mod 后加入配置表，下一次游玩整合包时可以直接复用。",
        "遇到启动问题时，逐个停用最近添加的 Mod，快速定位冲突来源。",
        "为每个实例保留清晰的名称和目录，日后管理大型 Mod 收藏更轻松。"
    };

    public HomeViewModel(
        InstanceScanner scanner,
        SettingsService settings,
        ProfileService profiles,
        LocalModLibraryService library,
        NavigationService nav)
    {
        _scanner = scanner;
        _settings = settings;
        _profiles = profiles;
        _library = library;
        _nav = nav;
    }

    public void LoadData()
    {
        try
        {
            var instances = _scanner.ScanAll(_settings.ScanRoots)
                .Where(instance => !_settings.IsExcluded(instance.DirectoryPath))
                .ToList();

            InstanceCount = instances.Count;

            _profiles.LoadAll();
            ProfileCount = _profiles.Profiles.Count;
            _library.Load();
            ModCount = _library.Mods.Count;
            SuggestionText = Suggestions[Random.Shared.Next(Suggestions.Length)];
            ScanStatus = instances.Count > 0
                ? $"已同步 {instances.Count} 个游戏实例"
                : "还没有发现实例，开始建立你的方块世界";
        }
        catch (Exception ex)
        {
            ScanStatus = $"同步失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenInstances() => _nav.Navigate<InstancesPage>();

    [RelayCommand]
    private void OpenProfiles() => _nav.Navigate<ProfilesPage>();

    [RelayCommand]
    private void OpenLibrary() => _nav.Navigate<LocalModLibraryPage>();

    [RelayCommand]
    private void OpenSettings() => _nav.Navigate<SettingsPage>();
}

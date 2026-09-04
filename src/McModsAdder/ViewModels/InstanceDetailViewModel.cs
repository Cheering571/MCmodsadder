using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;
using MCModPlus.Services;
using MCModPlus.Views;

namespace MCModPlus.ViewModels;

public partial class InstanceDetailViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly ProfileService _profileService;
    private readonly ModJarAnalyzer _analyzer;
    private readonly ModInstaller _installer;
    private readonly NavigationService _nav;

    [ObservableProperty]
    private GameInstance? _instance;

    [ObservableProperty]
    private ObservableCollection<ModProfile> _profiles = new();

    [ObservableProperty]
    private ModProfile? _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledModsHeader))]
    private ObservableCollection<InstalledMod> _installedMods = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledModsHeader))]
    private bool _hasRecognizedMods;

    public string InstalledModsHeader => HasRecognizedMods
        ? $"共计 {InstalledMods.Count} 个 mod"
        : "mod 尚未识别完成";

    [ObservableProperty]
    private ObservableCollection<ComparisonRow> _rows = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private int _installedCount;

    [ObservableProperty]
    private int _missingCount;

    [ObservableProperty]
    private int _unavailableCount;

    [ObservableProperty]
    private bool _hasCompared;

    [ObservableProperty]
    private bool _canInstall;

    private bool _modsReady;

    public InstanceDetailViewModel(
        AppState appState,
        ProfileService profileService,
        ModJarAnalyzer analyzer,
        ModInstaller installer,
        NavigationService nav)
    {
        _appState = appState;
        _profileService = profileService;
        _analyzer = analyzer;
        _installer = installer;
        _nav = nav;
    }

    public async Task InitializeAsync()
    {
        Instance = _appState.CurrentInstance;
        if (Instance == null)
        {
            return;
        }

        _profileService.LoadAll();
        Profiles = new ObservableCollection<ModProfile>(_profileService.Profiles);

        await RefreshModsAsync();
        SelectedProfile = Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RefreshModsAsync()
    {
        if (Instance == null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        HasRecognizedMods = false;
        ErrorText = string.Empty;
        try
        {
            var progress = new Progress<ModScanProgress>(p => BusyText = $"{p.Stage}… {p.Percentage}%");
            await _analyzer.AnalyzeAsync(Instance, progress);
            InstalledMods = new ObservableCollection<InstalledMod>(Instance.InstalledMods);
            HasRecognizedMods = true;
            _modsReady = true;
            if (SelectedProfile != null)
            {
                await CompareAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorText = $"识别已安装 mod 失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    async partial void OnSelectedProfileChanged(ModProfile? value)
    {
        if (value != null && Instance != null && _modsReady)
        {
            await CompareAsync();
        }
    }

    private async Task CompareAsync()
    {
        if (Instance == null || SelectedProfile == null)
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        BusyText = "正在与配置表对比…";
        try
        {
            var (rows, plan, unavailable) = await _installer.BuildPlanAsync(Instance, SelectedProfile);
            Rows = new ObservableCollection<ComparisonRow>(rows);
            BusyText = "正在加载 Mod 缩略图…";
            _appState.LastComparison = rows;
            _appState.LastPlan = plan;
            _appState.LastUnavailable = unavailable;

            InstalledCount = rows.Count(r => r.Status == ComparisonStatus.Installed);
            MissingCount = rows.Count(r => r.Status == ComparisonStatus.Missing);
            UnavailableCount = unavailable.Count;
            HasCompared = true;
            CanInstall = plan.Count > 0;

            var iconTasks = rows.Select(LoadRowIconAsync).ToArray();
            await Task.WhenAll(iconTasks);
        }
        catch (Exception ex)
        {
            ErrorText = $"对比失败（请检查网络或在设置中切换镜像源）：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    [RelayCommand]
    private void StartInstall()
    {
        if (_appState.LastPlan == null || _appState.LastPlan.Count == 0)
        {
            return;
        }
        _nav.Navigate<InstallPage>();
    }

    [RelayCommand]
    private void GoProfiles() => _nav.Navigate<ProfilesPage>();

    [RelayCommand]
    private void Back() => _nav.Navigate<InstancesPage>();

    private static async Task LoadRowIconAsync(ComparisonRow row)
    {
        row.Icon = await ImageLoader.GetAsync(row.Entry.IconUrl);
    }
}

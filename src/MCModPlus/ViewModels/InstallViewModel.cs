using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MCModPlus.Models;
using MCModPlus.Services;
using MCModPlus.Views;

namespace MCModPlus.ViewModels;

/// <summary>安装列表行（UI 实时更新用）</summary>
public partial class InstallItemRow : ObservableObject
{
    public InstallItem Item { get; }

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = "等待中";

    [ObservableProperty]
    private int _state; // 0等待 1下载中 2成功 3失败

    public string Name => string.IsNullOrEmpty(Item.ProjectName) ? Item.Version.FileName : Item.ProjectName;
    public string VersionText => Item.Version.VersionNumber;
    public string KindText => Item.Kind == InstallItemKind.Dependency ? "依赖" : "配置表";
    public bool IsDependency => Item.Kind == InstallItemKind.Dependency;
    public string? Error => Item.Error;

    public InstallItemRow(InstallItem item)
    {
        Item = item;
    }

    public void SyncFromItem()
    {
        Progress = Item.Progress * 100;
        State = Item.Status switch
        {
            InstallItemStatus.Downloading => 1,
            InstallItemStatus.Success => 2,
            InstallItemStatus.Failed => 3,
            _ => 0
        };
        StatusText = Item.Status switch
        {
            InstallItemStatus.Downloading => "下载中",
            InstallItemStatus.Success => "完成",
            InstallItemStatus.Failed => "失败",
            _ => "等待中"
        };
        OnPropertyChanged(nameof(Error));
    }
}

public partial class InstallViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly ModInstaller _installer;
    private readonly NavigationService _nav;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _syncTimer;

    [ObservableProperty]
    private GameInstance? _instance;

    [ObservableProperty]
    private ObservableCollection<InstallItemRow> _items = new();

    [ObservableProperty]
    private ObservableCollection<ProfileEntry> _unavailable = new();

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private bool _canStart;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private string? _backupDir;

    public InstallViewModel(AppState appState, ModInstaller installer, NavigationService nav)
    {
        _appState = appState;
        _installer = installer;
        _nav = nav;
    }

    public void LoadData()
    {
        Instance = _appState.CurrentInstance;
        var plan = _appState.LastPlan ?? new List<InstallItem>();
        Items = new ObservableCollection<InstallItemRow>(plan.Select(p => new InstallItemRow(p)));
        Unavailable = new ObservableCollection<ProfileEntry>(_appState.LastUnavailable ?? new List<ProfileEntry>());
        CanStart = Items.Count > 0;
        IsFinished = false;
        OverallProgress = 0;
        StatusText = Items.Count > 0
            ? $"共 {Items.Count} 个文件待下载（含 {Items.Count(i => i.IsDependency)} 个依赖）"
            : "没有需要安装的 mod";
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (Instance == null || IsRunning)
        {
            return;
        }

        var plan = Items.Select(r => r.Item).ToList();
        IsRunning = true;
        CanStart = false;
        _cts = new CancellationTokenSource();

        // 定时同步各行进度到 UI
        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _syncTimer.Tick += (_, _) =>
        {
            foreach (var row in Items)
            {
                row.SyncFromItem();
            }
        };
        _syncTimer.Start();

        var progress = new Progress<InstallProgress>(p =>
        {
            OverallProgress = plan.Count > 0 ? (double)p.Completed / plan.Count * 100 : 0;
            StatusText = $"正在下载 {p.CurrentFile}（{p.Completed}/{p.Total}）";
        });

        try
        {
            var result = await _installer.ExecuteAsync(Instance, plan, progress, _cts.Token);
            BackupDir = result.BackupDir;
            var failureDetails = result.Failed.Count == 0
                ? string.Empty
                : "\n" + string.Join("\n", result.Failed.Select(item => $"{item.Version.FileName}：{item.Error ?? "未知错误"}"));
            ResultText = result.Cancelled
                ? $"已取消：成功 {result.Succeeded.Count} 个，失败 {result.Failed.Count} 个{failureDetails}"
                : result.Failed.Count == 0
                    ? $"全部完成！成功安装 {result.Succeeded.Count} 个 mod"
                    : $"完成：成功 {result.Succeeded.Count} 个，失败 {result.Failed.Count} 个{failureDetails}";
        }
        catch (Exception ex)
        {
            ResultText = $"安装过程出错：{ex.Message}";
        }
        finally
        {
            _syncTimer.Stop();
            foreach (var row in Items)
            {
                row.SyncFromItem();
            }
            OverallProgress = 100;
            IsRunning = false;
            IsFinished = true;
            StatusText = string.Empty;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void BackToInstance() => _nav.Navigate<InstanceDetailPage>();

    [RelayCommand]
    private void BackToInstances() => _nav.Navigate<InstancesPage>();
}

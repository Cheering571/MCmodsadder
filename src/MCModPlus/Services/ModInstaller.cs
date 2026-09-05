using System.IO;
using System.Net.Http;
using MCModPlus.Models;

namespace MCModPlus.Services;

public enum InstallItemKind
{
    Profile,
    Dependency
}

public enum InstallItemStatus
{
    Pending,
    Downloading,
    Success,
    Failed
}

/// <summary>安装计划中的一个待下载文件</summary>
public class InstallItem
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public ModVersionInfo Version { get; set; } = new();
    public string? LocalFilePath { get; set; }
    public InstallItemKind Kind { get; set; }
    public InstallItemStatus Status { get; set; } = InstallItemStatus.Pending;
    public string? Error { get; set; }
    public double Progress { get; set; }
}

/// <summary>配置表条目的对比结果</summary>
public partial class ComparisonRow : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public ProfileEntry Entry { get; set; } = new();
    public ComparisonStatus Status { get; set; }
    public InstalledMod? Installed { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private System.Windows.Media.Imaging.BitmapImage? _icon;

    public string VersionText =>
        Status == ComparisonStatus.Installed && Installed != null
            ? Installed.DisplayVersion
            : string.Empty;
}

public enum ComparisonStatus
{
    Installed,
    Missing,
    Unavailable
}

public class InstallResult
{
    public List<InstallItem> Succeeded { get; } = new();
    public List<InstallItem> Failed { get; } = new();
    public List<ProfileEntry> Unavailable { get; } = new();
    public string? BackupDir { get; set; }
    public bool Cancelled { get; set; }
}

public class InstallProgress
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public InstallItem? CurrentItem { get; set; }
}

/// <summary>
/// 核心安装器：缺失计算、BFS 依赖闭包、并发下载、校验落盘、备份。
/// </summary>
public class ModInstaller
{
    private readonly IModProvider _provider;
    private readonly SettingsService _settings;
    private readonly LocalModLibraryService _localLibrary;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ModVersionInfo> _versionCache = new(StringComparer.OrdinalIgnoreCase);

    public ModInstaller(IModProvider provider, SettingsService settings, LocalModLibraryService localLibrary)
    {
        _provider = provider;
        _settings = settings;
        _localLibrary = localLibrary;
    }

    /// <summary>
    /// 计算配置表对比结果与安装计划。
    /// 返回 (对比行列表, 待安装清单含依赖, 不可用条目)。
    /// </summary>
    public async Task<(List<ComparisonRow> Rows, List<InstallItem> Plan, List<ProfileEntry> Unavailable)> BuildPlanAsync(
        GameInstance instance, ModProfile profile, bool forceLocalMods = false, CancellationToken ct = default)
    {
        var installedByProjectId = instance.InstalledMods
            .Where(m => !string.IsNullOrWhiteSpace(m.ProjectId))
            .GroupBy(m => m.ProjectId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var installedByFileName = instance.InstalledMods
            .Where(m => !string.IsNullOrWhiteSpace(m.FileName))
            .GroupBy(m => m.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var installedProjectIds = installedByProjectId.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<ComparisonRow>();
        var plan = new List<InstallItem>();
        var unavailable = new List<ProfileEntry>();
        var plannedProjectIds = new HashSet<string>();
        var remoteVersions = (await Task.WhenAll(profile.Entries
            .Where(entry => string.IsNullOrWhiteSpace(entry.LocalModId) && !string.IsNullOrWhiteSpace(entry.ProjectId))
            .Select(entry => GetBestVersionCachedAsync(entry.ProjectId, instance.GameVersion, instance.Loader, ct))))
            .GroupBy(result => result.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Version, StringComparer.OrdinalIgnoreCase);

        // ---- 第一轮：配置表条目 ----
        foreach (var entry in profile.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var installed = installedByProjectId.GetValueOrDefault(entry.ProjectId);
            if (installed == null && string.IsNullOrWhiteSpace(entry.LocalModId)
                && remoteVersions.TryGetValue(entry.ProjectId, out var matchingVersion))
            {
                installed = installedByFileName.GetValueOrDefault(matchingVersion?.FileName ?? string.Empty);
            }
            var libraryMod = string.IsNullOrWhiteSpace(entry.LocalModId) ? null : _localLibrary.GetById(entry.LocalModId);
            if (libraryMod != null)
            {
                if (!IsCompatible(libraryMod, entry, instance) && !forceLocalMods)
                {
                    rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Unavailable });
                    unavailable.Add(entry);
                    continue;
                }

                var local = instance.InstalledMods.FirstOrDefault(m => string.Equals(m.FileName, libraryMod.FileName, StringComparison.OrdinalIgnoreCase));
                if (local != null)
                {
                    rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Installed, Installed = local });
                    continue;
                }

                rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Missing });
                plan.Add(new InstallItem
                {
                    ProjectId = libraryMod.Id,
                    ProjectName = entry.Name,
                    LocalFilePath = _localLibrary.GetStoredPath(libraryMod),
                    Version = new ModVersionInfo { FileName = libraryMod.FileName, VersionNumber = libraryMod.Version },
                    Kind = InstallItemKind.Profile
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.LocalModId))
            {
                rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Unavailable });
                unavailable.Add(entry);
                continue;
            }

            if (installed != null)
            {
                rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Installed, Installed = installed });
                continue;
            }

            var version = remoteVersions.GetValueOrDefault(entry.ProjectId);
            if (version == null)
            {
                rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Unavailable });
                unavailable.Add(entry);
                continue;
            }

            rows.Add(new ComparisonRow { Entry = entry, Status = ComparisonStatus.Missing });
            plan.Add(new InstallItem
            {
                ProjectId = entry.ProjectId,
                ProjectName = entry.Name,
                Version = version,
                Kind = InstallItemKind.Profile
            });
            plannedProjectIds.Add(entry.ProjectId);
        }

        // ---- 第二轮：BFS 依赖闭包 ----
        if (_settings.Current.AutoInstallDependencies)
        {
            var queue = new Queue<InstallItem>(plan);
            var visited = new HashSet<string>(plannedProjectIds);

            while (queue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var item = queue.Dequeue();
                foreach (var dep in item.Version.Dependencies.Where(d => d.Required && d.ProjectId != null))
                {
                    var depId = dep.ProjectId!;
                    if (!visited.Add(depId))
                    {
                        continue; // 已处理过，防循环
                    }
                    if (installedProjectIds.Contains(depId))
                    {
                        continue; // 已装，跳过
                    }

                    var depVersion = await _provider.GetBestVersionAsync(depId, instance.GameVersion, instance.Loader, ct);
                    if (depVersion == null)
                    {
                        continue; // 依赖无匹配版本：加入不可用提示
                    }

                    var depNames = await _provider.GetProjectNamesAsync(new[] { depId }, ct);
                    depNames.TryGetValue(depId, out var depName);

                    var depItem = new InstallItem
                    {
                        ProjectId = depId,
                        ProjectName = depName ?? depId,
                        Version = depVersion,
                        Kind = InstallItemKind.Dependency
                    };
                    plan.Add(depItem);
                    plannedProjectIds.Add(depId);
                    queue.Enqueue(depItem);
                }
            }
        }

        return (rows, plan, unavailable);
    }

    private async Task<(string ProjectId, ModVersionInfo? Version)> GetBestVersionCachedAsync(
        string projectId, string gameVersion, ModLoader loader, CancellationToken ct)
    {
        var cacheKey = $"{projectId}|{gameVersion}|{loader}";
        if (_versionCache.TryGetValue(cacheKey, out var cached))
        {
            return (projectId, cached);
        }

        var version = await _provider.GetBestVersionAsync(projectId, gameVersion, loader, ct);
        if (version != null)
        {
            _versionCache[cacheKey] = version;
        }
        return (projectId, version);
    }

    private static bool IsCompatible(LocalMod libraryMod, ProfileEntry entry, GameInstance instance)
    {
        var loader = libraryMod.Loader;
        var gameVersion = libraryMod.GameVersion;

        var loaderMatches = loader == ModLoader.Unknown || instance.Loader == ModLoader.Unknown || loader == instance.Loader;
        var versionMatches = string.IsNullOrWhiteSpace(gameVersion) || gameVersion == "未知"
            || string.IsNullOrWhiteSpace(instance.GameVersion)
            || string.Equals(gameVersion, instance.GameVersion, StringComparison.OrdinalIgnoreCase);
        return loaderMatches && versionMatches;
    }

    /// <summary>
    /// 执行安装计划：并发下载 → sha1 校验 → 落盘 mods 目录。
    /// </summary>
    public async Task<InstallResult> ExecuteAsync(
        GameInstance instance,
        List<InstallItem> plan,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new InstallResult();
        Directory.CreateDirectory(instance.ModsPath);

        // 备份：把同名校验不一致的旧文件移入备份目录
        if (_settings.Current.BackupEnabled)
        {
            result.BackupDir = await BackupConflictsAsync(instance, plan, ct);
        }

        var maxConcurrency = Math.Max(1, _settings.Current.DownloadConcurrency);
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var completed = 0;
        var lockObj = new object();
        var destinationLocks = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        var tasks = plan.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                item.Status = InstallItemStatus.Downloading;
                lock (lockObj)
                {
                    progress?.Report(new InstallProgress
                    {
                        Completed = completed,
                        Total = plan.Count,
                        CurrentFile = item.Version.FileName,
                        CurrentItem = item
                    });
                }

                var destPath = Path.Combine(instance.ModsPath, item.Version.FileName);
                var itemProgress = new Progress<double>(p => item.Progress = p);
                var destinationLock = destinationLocks.GetOrAdd(destPath, _ => new SemaphoreSlim(1, 1));
                await destinationLock.WaitAsync(ct);
                try
                {
                    if (!string.IsNullOrWhiteSpace(item.LocalFilePath))
                    {
                        if (!File.Exists(item.LocalFilePath))
                        {
                            throw new FileNotFoundException("本地 Mod 托管文件不存在。", item.LocalFilePath);
                        }

                        File.Copy(item.LocalFilePath, destPath, overwrite: true);
                        item.Progress = 1;
                    }
                    else
                    {
                        await _provider.DownloadAsync(item.Version, destPath, itemProgress, ct);
                    }
                }
                finally
                {
                    destinationLock.Release();
                }

                item.Status = InstallItemStatus.Success;
                lock (lockObj) { result.Succeeded.Add(item); }
            }
            catch (OperationCanceledException)
            {
                item.Status = InstallItemStatus.Failed;
                item.Error = "已取消";
                lock (lockObj) { result.Failed.Add(item); result.Cancelled = true; }
                throw;
            }
            catch (Exception ex)
            {
                item.Status = InstallItemStatus.Failed;
                item.Error = ex is HttpRequestException
                    ? ex.Message
                    : $"{ex.GetType().Name}: {ex.Message}";
                try
                {
                    Directory.CreateDirectory(SettingsService.DataDir);
                    File.AppendAllText(
                        Path.Combine(SettingsService.DataDir, "download.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 项目={item.ProjectName}; 文件={item.Version.FileName}; 项目ID={item.ProjectId}; 地址={item.Version.DownloadUrl}; 错误={ex}\n\n");
                }
                catch
                {
                }
                lock (lockObj) { result.Failed.Add(item); }
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref completed);
                lock (lockObj)
                {
                    progress?.Report(new InstallProgress
                    {
                        Completed = current,
                        Total = plan.Count,
                        CurrentFile = item.Version.FileName,
                        CurrentItem = item
                    });
                }
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        return result;
    }

    /// <summary>
    /// 将 mods 目录中与待装文件同名但内容不同的旧文件移入备份目录。
    /// </summary>
    private static async Task<string?> BackupConflictsAsync(GameInstance instance, List<InstallItem> plan, CancellationToken ct)
    {
        string? backupDir = null;
        foreach (var item in plan)
        {
            var existing = Path.Combine(instance.ModsPath, item.Version.FileName);
            if (!File.Exists(existing))
            {
                continue;
            }

            var existingSha1 = await Providers.ModrinthProvider.ComputeSha1Async(existing, ct);
            if (existingSha1.Equals(item.Version.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                continue; // 内容一致，无需备份
            }

            backupDir ??= Path.Combine(instance.ModsPath,
                ".MCModPlus-backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backupDir);
            await MoveWithRetryAsync(existing, Path.Combine(backupDir, item.Version.FileName), ct);
        }
        return backupDir;
    }

    private static async Task MoveWithRetryAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        IOException? lastException = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException ex) when (attempt < 7)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), ct);
            }
        }

        throw lastException!;
    }
}

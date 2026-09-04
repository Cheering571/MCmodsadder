using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCModPlus.Models;

namespace MCModPlus.Services;

public class SettingsService
{
    private const string LegacyAppName = "McModsAdder";

    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCModPlus");

    private static readonly string LegacyDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyAppName);

    private static readonly string SettingsPath = Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    /// <summary>手动添加过的扫描根目录（.minecraft 目录或实例目录）</summary>
    public List<string> ScanRoots { get; private set; } = new();

    /// <summary>仅在软件中隐藏的实例目录，绝不删除对应文件。</summary>
    public List<string> ExcludedInstancePaths { get; private set; } = new();

    public void Load()
    {
        try
        {
            MigrateLegacyData();
            var legacy = LoadSettingsFile(Path.Combine(LegacyDataDir, "settings.json"));
            var current = LoadSettingsFile(SettingsPath);
            var settings = current ?? legacy;

            if (settings != null)
            {
                Current = settings.Settings ?? new AppSettings();
                var protectedApiKey = Current.CurseForgeApiKeyProtected;
                if (!string.IsNullOrWhiteSpace(protectedApiKey))
                {
                    Current.CurseForgeApiKey = UnprotectApiKey(protectedApiKey) ?? string.Empty;
                }

                ScanRoots = MergePaths(legacy?.ScanRoots, current?.ScanRoots);
                ExcludedInstancePaths = MergePaths(legacy?.ExcludedInstancePaths, current?.ExcludedInstancePaths);
            }
        }
        catch
        {
            Current = new AppSettings();
            ScanRoots = new List<string>();
            ExcludedInstancePaths = new List<string>();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            Current.CurseForgeApiKeyProtected = string.IsNullOrWhiteSpace(Current.CurseForgeApiKey)
                ? null
                : ProtectApiKey(Current.CurseForgeApiKey);

            var doc = new SettingsFile
            {
                Settings = Current,
                ScanRoots = ScanRoots,
                ExcludedInstancePaths = ExcludedInstancePaths
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(doc, JsonOpts));
        }
        catch
        {
            // 设置保存失败不阻断应用
        }
    }

    public void AddScanRoot(string path)
    {
        var normalized = NormalizePath(path);
        if (normalized == null)
        {
            return;
        }

        if (!ScanRoots.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            ScanRoots.Add(normalized);
            Save();
        }
    }

    public void ExcludeInstance(string path)
    {
        var normalized = NormalizePath(path);
        if (normalized == null)
        {
            return;
        }

        if (!ExcludedInstancePaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedInstancePaths.Add(normalized);
            Save();
        }
    }

    public bool IsExcluded(string path)
    {
        var normalized = NormalizePath(path);
        return normalized != null && ExcludedInstancePaths.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception) when (path.Trim().Length > 0)
        {
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static SettingsFile? LoadSettingsFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<SettingsFile>(json);
        if (settings?.Settings != null
            && string.IsNullOrWhiteSpace(settings.Settings.CurseForgeApiKeyProtected))
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("Settings", out var settingsElement)
                && settingsElement.TryGetProperty("CurseForgeApiKey", out var apiKeyElement)
                && apiKeyElement.ValueKind == JsonValueKind.String)
            {
                settings.Settings.CurseForgeApiKey = apiKeyElement.GetString() ?? string.Empty;
            }
        }

        return settings;
    }

    private static string ProtectApiKey(string value)
    {
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? UnprotectApiKey(string value)
    {
        try
        {
            var protectedBytes = Convert.FromBase64String(value);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static List<string> MergePaths(params IEnumerable<string>?[] pathLists)
    {
        return pathLists
            .Where(paths => paths != null)
            .SelectMany(paths => paths!)
            .Select(NormalizePath)
            .Where(path => path != null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void MigrateLegacyData()
    {
        if (!Directory.Exists(LegacyDataDir))
        {
            return;
        }

        Directory.CreateDirectory(DataDir);
        foreach (var source in Directory.EnumerateFiles(LegacyDataDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(LegacyDataDir, source);
            var target = Path.Combine(DataDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
    }

    private class SettingsFile
    {
        public AppSettings? Settings { get; set; }
        public List<string>? ScanRoots { get; set; }
        public List<string>? ExcludedInstancePaths { get; set; }
    }
}

using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using MCModPlus.Models;

namespace MCModPlus.Services;

/// <summary>
/// 扫描与识别 Minecraft 实例（兼容 PCL / HMCL 版本隔离目录）。
/// </summary>
public class InstanceScanner
{
    // 1.21.1 为传统版本号；26.2 起为新版年份版本号。仅匹配这两种正式 MC 版本格式，避免将加载器版本误识别为游戏版本。
    private static readonly Regex McVersionRegex = new(@"(?<!\d)(?:1\.\d+(?:\.\d+)?|\d{2}\.\d+)(?!\d)", RegexOptions.Compiled);

    /// <summary>
    /// 扫描所有已知启动器目录。扫描范围限定为启动器常用目录和用户配置目录，避免遍历整个磁盘造成卡顿及误识别。
    /// </summary>
    public List<GameInstance> ScanAll(IEnumerable<string>? customRoots = null)
    {
        var roots = GetDefaultScanRoots();
        if (customRoots != null)
        {
            roots.AddRange(customRoots);
        }

        var found = new Dictionary<string, GameInstance>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var instance in Scan(root))
            {
                var key = NormalizePath(instance.DirectoryPath);
                found[key] = instance;
            }
        }

        return found.Values
            .OrderByDescending(i => i.IsModded)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>返回官方、HMCL、PCL、MultiMC/Prism、BakaXL 等启动器的常见实例根目录。</summary>
    public static List<string> GetDefaultScanRoots()
    {
        var roots = new List<string>();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        AddExisting(roots, Path.Combine(appData, ".minecraft"));
        AddExisting(roots, Path.Combine(appData, "Microsoft Launcher", "minecraft"));
        AddExisting(roots, Path.Combine(appData, "hmcl"));
        AddExisting(roots, Path.Combine(appData, "HMCL"));
        AddExisting(roots, Path.Combine(appData, "PCL"));
        AddExisting(roots, Path.Combine(appData, "PCL2"));
        AddExisting(roots, Path.Combine(appData, "BakaXL"));
        AddExisting(roots, Path.Combine(appData, "MultiMC", "instances"));
        AddExisting(roots, Path.Combine(appData, "PrismLauncher", "instances"));
        AddExisting(roots, Path.Combine(localAppData, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Roaming", ".minecraft"));
        AddExisting(roots, Path.Combine(userProfile, ".minecraft"));
        AddExisting(roots, Path.Combine(documents, "Minecraft", "instances"));
        AddExisting(roots, Path.Combine(documents, "MultiMC", "instances"));
        AddExisting(roots, Path.Combine(documents, "PrismLauncher", "instances"));

        return roots;
    }

    private static void AddExisting(ICollection<string> roots, string path)
    {
        if (Directory.Exists(path))
        {
            roots.Add(path);
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>
    /// 扫描给定根路径，支持 .minecraft、versions、instances、单实例目录及其一层嵌套结构。
    /// </summary>
    public List<GameInstance> Scan(string rootPath)
    {
        var result = new Dictionary<string, GameInstance>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return new();
        }

        var root = NormalizePath(rootPath);
        AddParsed(result, root);
        ScanKnownChild(root, "versions", result);
        ScanKnownChild(root, "instances", result);
        ScanKnownChild(Path.Combine(root, ".minecraft"), "versions", result);
        ScanKnownChild(Path.Combine(root, ".minecraft"), "instances", result);

        var rootName = Path.GetFileName(root);
        if (rootName.Equals("versions", StringComparison.OrdinalIgnoreCase)
            || rootName.Equals("instances", StringComparison.OrdinalIgnoreCase))
        {
            ScanDirectories(root, result);
        }

        return result.Values
            .OrderByDescending(i => i.IsModded)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ScanKnownChild(string parent, string childName, IDictionary<string, GameInstance> result)
    {
        var child = Path.Combine(parent, childName);
        if (Directory.Exists(child))
        {
            ScanDirectories(child, result);
        }
    }

    private void ScanDirectories(string path, IDictionary<string, GameInstance> result)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                AddParsed(result, dir);
                var nestedMinecraft = Path.Combine(dir, ".minecraft");
                if (Directory.Exists(nestedMinecraft))
                {
                    AddParsed(result, nestedMinecraft);
                    ScanKnownChild(nestedMinecraft, "versions", result);
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private void AddParsed(IDictionary<string, GameInstance> result, string path)
    {
        var instance = ParseInstanceDir(path);
        if (instance != null)
        {
            result[NormalizePath(instance.DirectoryPath)] = instance;
        }
    }

    /// <summary>返回默认的 .minecraft 目录（存在时）。</summary>
    public static string? GetDefaultMinecraftDir()
    {
        return GetDefaultScanRoots().FirstOrDefault(p =>
            Path.GetFileName(p).Equals(".minecraft", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 解析单个实例目录：读取 &lt;目录名&gt;.json 判定 MC 版本与加载器。
    /// </summary>
    public GameInstance? ParseInstanceDir(string dir)
    {
        try
        {
            return ParseInstanceDirCore(dir);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private GameInstance? ParseInstanceDirCore(string dir)
    {
        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var jsonPath = Path.Combine(dir, name + ".json");
        var modsPath = Path.Combine(dir, "mods");

        // 没有版本 json 也没有 mods 目录的，不认为是实例
        if (!File.Exists(jsonPath) && !Directory.Exists(modsPath))
        {
            return null;
        }

        var instance = new GameInstance
        {
            Name = name,
            DirectoryPath = dir,
            ModsPath = modsPath
        };

        if (File.Exists(jsonPath))
        {
            try
            {
                ParseVersionJson(File.ReadAllText(jsonPath), instance);
            }
            catch
            {
                // json 解析失败时仍保留实例（版本/加载器未知）
            }
        }

        // 只有纯目录（无 json）时尝试从目录名推断 MC 版本（最后兜底）
        if (string.IsNullOrEmpty(instance.GameVersion))
        {
            var m = McVersionRegex.Match(name);
            if (m.Success)
            {
                instance.GameVersion = m.Value;
            }
        }

        return instance;
    }

    private static void ParseVersionJson(string json, GameInstance instance)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var id = GetStr(root, "id");
        var inheritsFrom = GetStr(root, "inheritsFrom");
        var mainClass = GetStr(root, "mainClass");
        var jarProp = GetStr(root, "jar");
        var clientVersion = GetStr(root, "clientVersion"); // PCL 合并 json 写入的 MC 版本

        var libraries = new List<string>();
        if (root.TryGetProperty("libraries", out var libsEl) && libsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var lib in libsEl.EnumerateArray())
            {
                if (lib.TryGetProperty("name", out var nameEl))
                {
                    var n = nameEl.GetString();
                    if (!string.IsNullOrEmpty(n))
                    {
                        libraries.Add(n);
                    }
                }
            }
        }

        // ============ 1. HMCL patches 格式（最可靠，优先） ============
        if (root.TryGetProperty("patches", out var patchesEl) && patchesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var patch in patchesEl.EnumerateArray())
            {
                var pid = GetStr(patch, "id");
                var pver = GetStr(patch, "version");
                switch (pid)
                {
                    case "game":
                        if (McVersionRegex.IsMatch(pver))
                        {
                            instance.GameVersion = McVersionRegex.Match(pver).Value;
                        }
                        break;
                    case "fabric":
                        instance.Loader = ModLoader.Fabric;
                        instance.LoaderVersion = pver;
                        break;
                    case "quilt":
                        instance.Loader = ModLoader.Quilt;
                        instance.LoaderVersion = pver;
                        break;
                    case "forge":
                        instance.Loader = ModLoader.Forge;
                        instance.LoaderVersion = pver;
                        break;
                    case "neoforge":
                        instance.Loader = ModLoader.NeoForge;
                        instance.LoaderVersion = pver;
                        break;
                }
            }
        }

        // ============ 2. 加载器检测（精确 artifact 坐标，避免 sponge-mixin 等误判） ============
        if (instance.Loader == ModLoader.Unknown)
        {
            var fabricLib = FindCoord(libraries, "net.fabricmc:fabric-loader:");
            var quiltLib = FindCoord(libraries, "org.quiltmc:quilt-loader:");
            var neoforgeLib = FindCoord(libraries, "net.neoforged:neoforge:");
            var fmlLib = FindCoord(libraries, "net.neoforged.fancymodloader:loader:");
            var forgeLib = FindCoord(libraries, "net.minecraftforge:forge:")
                           ?? FindCoord(libraries, "net.minecraftforge:fmlloader:");

            if (fabricLib != null)
            {
                instance.Loader = ModLoader.Fabric;
                instance.LoaderVersion = CoordVersion(fabricLib);
            }
            else if (quiltLib != null)
            {
                instance.Loader = ModLoader.Quilt;
                instance.LoaderVersion = CoordVersion(quiltLib);
            }
            else if (neoforgeLib != null || fmlLib != null
                     || libraries.Any(l => l.StartsWith("net.neoforged:", StringComparison.OrdinalIgnoreCase)
                                        || l.StartsWith("net.neoforged.fancymodloader:", StringComparison.OrdinalIgnoreCase)))
            {
                instance.Loader = ModLoader.NeoForge;
                instance.LoaderVersion = CoordVersion(neoforgeLib ?? fmlLib!);
            }
            else if (forgeLib != null)
            {
                instance.Loader = ModLoader.Forge;
                var v = CoordVersion(forgeLib);
                // forge 坐标版本形如 1.20.1-47.2.0，取后半部分
                var dash = v.IndexOf('-');
                instance.LoaderVersion = dash >= 0 ? v[(dash + 1)..] : v;
            }
            else if (mainClass.Contains("fabricmc", StringComparison.OrdinalIgnoreCase))
            {
                instance.Loader = ModLoader.Fabric;
            }
            else if (mainClass.Contains("quiltmc", StringComparison.OrdinalIgnoreCase))
            {
                instance.Loader = ModLoader.Quilt;
            }
            // 注意：cpw.mods 是 Forge 与 NeoForge 共用的 mainClass，不能单独作为判据
        }

        // ============ 3. MC 版本检测（多源优先级） ============
        if (string.IsNullOrEmpty(instance.GameVersion))
        {
            instance.GameVersion = FirstMcVersion(
                clientVersion,   // PCL 合并 json 的显式字段，最优先
                inheritsFrom,    // 继承式 json
                jarProp);        // 部分启动器写入的 jar 字段
        }

        if (string.IsNullOrEmpty(instance.GameVersion))
        {
            // net.minecraft:client:1.21.1-xxx / com.mojang:minecraft:1.21.1
            var clientLib = libraries.FirstOrDefault(l =>
                l.StartsWith("net.minecraft:client:", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("com.mojang:minecraft:", StringComparison.OrdinalIgnoreCase));
            if (clientLib != null)
            {
                instance.GameVersion = FirstMcVersion(CoordVersion(clientLib));
            }
        }

        if (string.IsNullOrEmpty(instance.GameVersion))
        {
            // net.minecraftforge:forge:1.20.1-47.2.0 → 取 '-' 前段
            var forgeLib = FindCoord(libraries, "net.minecraftforge:forge:");
            if (forgeLib != null)
            {
                var v = CoordVersion(forgeLib);
                var dash = v.IndexOf('-');
                instance.GameVersion = FirstMcVersion(dash > 0 ? v[..dash] : v);
            }
        }

        if (string.IsNullOrEmpty(instance.GameVersion))
        {
            // NeoForge 版本号映射 MC 版本（21.1.x → 1.21.1，47.x → 1.20.1）
            var neoforgeLib = FindCoord(libraries, "net.neoforged:neoforge:");
            if (neoforgeLib != null)
            {
                instance.GameVersion = MapNeoForgeToMc(CoordVersion(neoforgeLib)) ?? string.Empty;
            }
        }

        if (string.IsNullOrEmpty(instance.GameVersion))
        {
            // 最后兜底：从 json id 匹配（可能误中整合包版本号，仅作保底）
            instance.GameVersion = FirstMcVersion(id);
        }
    }

    /// <summary>从若干候选字符串中取第一个匹配 MC 版本格式的值</summary>
    private static string FirstMcVersion(params string[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c))
            {
                var m = McVersionRegex.Match(c);
                if (m.Success)
                {
                    return m.Value;
                }
            }
        }
        return string.Empty;
    }

    private static string? FindCoord(List<string> libraries, string prefix) =>
        libraries.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>坐标格式 group:artifact:version[:classifier]，取版本段</summary>
    private static string CoordVersion(string coord)
    {
        var parts = coord.Split(':');
        return parts.Length >= 3 ? parts[2] : string.Empty;
    }

    private static string GetStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// NeoForge 版本 → MC 版本。NeoForge 自 1.20.2 起版本号为 MC 次版本号（去掉 "1."）：
    /// 20.2.x → 1.20.2，21.1.x → 1.21.1；例外：47.x → 1.20.1。
    /// </summary>
    private static string? MapNeoForgeToMc(string neoforgeVer)
    {
        var m = Regex.Match(neoforgeVer, @"^(\d+)\.(\d+)");
        if (!m.Success)
        {
            return null;
        }
        if (m.Groups[1].Value == "47")
        {
            return "1.20.1";
        }
        // 次版本为 0 时 MC 版本不带 .0（如 1.21）
        return m.Groups[2].Value == "0"
            ? $"1.{m.Groups[1].Value}"
            : $"1.{m.Groups[1].Value}.{m.Groups[2].Value}";
    }

    /// <summary>
    /// 枚举 mods 目录中的 jar 文件（含一级子目录，跳过 .disabled 与隐藏目录）。
    /// </summary>
    public static List<string> EnumerateModFiles(string modsPath)
    {
        var files = new List<string>();
        if (!Directory.Exists(modsPath))
        {
            return files;
        }

        files.AddRange(Directory.GetFiles(modsPath, "*.jar"));

        foreach (var subDir in Directory.GetDirectories(modsPath))
        {
            var dirName = Path.GetFileName(subDir);
            if (dirName.StartsWith('.'))
            {
                continue; // 跳过 .backup 等隐藏目录
            }
            files.AddRange(Directory.GetFiles(subDir, "*.jar"));
        }

        return files;
    }
}

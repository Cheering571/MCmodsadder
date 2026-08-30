using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using McModsAdder.Models;

namespace McModsAdder.Services;

/// <summary>
/// 扫描与识别 Minecraft 实例（兼容 PCL / HMCL 版本隔离目录）。
/// </summary>
public class InstanceScanner
{
    private static readonly Regex McVersionRegex = new(@"1\.\d+(\.\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// 扫描给定根路径，返回发现的实例列表。
    /// 根路径可以是 .minecraft 目录（含 versions 子目录）、versions 目录本身、或单个实例目录。
    /// </summary>
    public List<GameInstance> Scan(string rootPath)
    {
        var result = new List<GameInstance>();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return result;
        }

        var versionsDir = Path.Combine(rootPath, "versions");
        if (Directory.Exists(versionsDir))
        {
            ScanVersionsDir(versionsDir, result);
        }
        else
        {
            // 常见整合包分发结构：用户选择的是整合包根目录，实际内容位于 .minecraft。
            var nestedMinecraft = Path.Combine(rootPath, ".minecraft");
            var nestedVersions = Path.Combine(nestedMinecraft, "versions");
            if (Directory.Exists(nestedVersions))
            {
                ScanVersionsDir(nestedVersions, result);
            }
            else if (Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar))
                         .Equals("versions", StringComparison.OrdinalIgnoreCase))
            {
                ScanVersionsDir(rootPath, result);
            }
            else
            {
                // 也许用户直接选了实例目录本身
                var single = ParseInstanceDir(rootPath);
                if (single != null)
                {
                    result.Add(single);
                }
                else
                {
                    // 再兜底：根目录下直接散落若干实例目录（某些整合包结构）
                    foreach (var dir in Directory.GetDirectories(rootPath))
                    {
                        var inst = ParseInstanceDir(dir);
                        if (inst != null)
                        {
                            result.Add(inst);
                        }
                    }
                }
            }
        }

        return result.OrderByDescending(i => i.IsModded).ThenBy(i => i.Name).ToList();
    }

    /// <summary>返回默认的 .minecraft 目录（存在时）</summary>
    public static string? GetDefaultMinecraftDir()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        return Directory.Exists(path) ? path : null;
    }

    private void ScanVersionsDir(string versionsDir, List<GameInstance> result)
    {
        foreach (var dir in Directory.GetDirectories(versionsDir))
        {
            var inst = ParseInstanceDir(dir);
            if (inst != null)
            {
                result.Add(inst);
            }
        }
    }

    /// <summary>
    /// 解析单个实例目录：读取 &lt;目录名&gt;.json 判定 MC 版本与加载器。
    /// </summary>
    public GameInstance? ParseInstanceDir(string dir)
    {
        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
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

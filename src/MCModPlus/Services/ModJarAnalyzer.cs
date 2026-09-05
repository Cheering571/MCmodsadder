using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using MCModPlus.Models;
using MCModPlus.Providers;

namespace MCModPlus.Services;

public sealed record ModScanProgress(string Stage, int Percentage);

/// <summary>
/// 识别 mods 目录中已安装的 mod：
/// 阶段一 并行 sha1 + Modrinth 批量哈希匹配（精确）；
/// 阶段二 未命中 jar 解析内部元数据兜底展示。
/// </summary>
public class ModJarAnalyzer
{
    private readonly IModProvider _provider;
    private string? _cachedInstancePath;
    private string? _cachedSignature;
    private List<InstalledMod>? _cachedMods;

    public ModJarAnalyzer(IModProvider provider)
    {
        _provider = provider;
    }

    public async Task AnalyzeAsync(GameInstance instance, IProgress<ModScanProgress>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(new ModScanProgress("正在遍历 mod 文件", 5));
        var files = InstanceScanner.EnumerateModFiles(instance.ModsPath).ToList();
        var signature = string.Join("|", files.Select(file =>
        {
            var info = new FileInfo(file);
            return $"{file}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        }));
        if (string.Equals(_cachedInstancePath, instance.ModsPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_cachedSignature, signature, StringComparison.Ordinal))
        {
            instance.InstalledMods = _cachedMods!.ToList();
            progress?.Report(new ModScanProgress("已使用缓存识别结果", 100));
            return;
        }

        var mods = files.Select(f => new InstalledMod
        {
            FileName = Path.GetFileName(f),
            FullPath = f
        }).ToList();

        progress?.Report(new ModScanProgress("正在计算 mod 哈希", 10));
        // ---- 阶段一：并行哈希 ----
        var semaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount, 8));
        var done = 0;
        var hashTasks = mods.Select(async mod =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                mod.Sha1 = await ModrinthProvider.ComputeSha1Async(mod.FullPath, ct);
            }
            catch
            {
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref done);
                progress?.Report(new ModScanProgress("正在计算 mod 哈希", 10 + current * 45 / Math.Max(mods.Count, 1)));
            }
        });
        await Task.WhenAll(hashTasks);

        progress?.Report(new ModScanProgress("正在匹配 Modrinth 项目并解析 CurseForge Mod", 60));

        // ---- 阶段二：批量哈希匹配 ----
        var hashToMod = mods.Where(m => !string.IsNullOrEmpty(m.Sha1))
                            .GroupBy(m => m.Sha1)
                            .ToDictionary(g => g.Key, g => g.First());
        if (hashToMod.Count > 0)
        {
            try
            {
                var matched = await _provider.MatchHashesAsync(hashToMod.Keys.ToList(), ct);
                var projectIds = matched.Values.Select(v => v.ProjectId).Distinct().ToList();
                var projectInfos = await _provider.GetProjectInfosAsync(projectIds, ct);

                foreach (var (hash, info) in matched)
                {
                    if (hashToMod.TryGetValue(hash, out var mod))
                    {
                        mod.ProjectId = info.ProjectId;
                        mod.MatchedVersionId = info.VersionId;
                        mod.MatchedVersionNumber = info.VersionNumber;
                        mod.IdentifyMethod = ModIdentifyMethod.Hash;
                        if (projectInfos.TryGetValue(info.ProjectId, out var projectInfo))
                        {
                            mod.IconUrl = projectInfo.IconUrl;
                            mod.ProjectName = projectInfo.Name;
                        }
                    }
                }
            }
            catch
            {
                // 网络失败时全部走元数据兜底
            }
        }

        // ---- 阶段三：未命中 jar 解析元数据 ----
        progress?.Report(new ModScanProgress("正在解析未匹配 mod 元数据", 75));
        var unmatched = mods.Where(m => m.IdentifyMethod == ModIdentifyMethod.None).ToList();
        var metaTasks = unmatched.Select(async mod =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ParseJarMetadata(mod);
            }
            catch
            {
                // 单个 jar 解析失败忽略
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(metaTasks);

        instance.InstalledMods = mods.OrderBy(m => m.DisplayName).ToList();
        _cachedInstancePath = instance.ModsPath;
        _cachedSignature = signature;
        _cachedMods = instance.InstalledMods.ToList();
        progress?.Report(new ModScanProgress("已完成 Mod 识别", 100));
    }

    /// <summary>解析 jar 内各加载器的元数据文件，填充兜底信息</summary>
    public static void ParseJarMetadata(InstalledMod mod)
    {
        using var zip = ZipFile.OpenRead(mod.FullPath);
        ParseManifest(zip, mod);

        // Fabric
        var fabricEntry = zip.GetEntry("fabric.mod.json");
        if (fabricEntry != null)
        {
            using var reader = new StreamReader(fabricEntry.Open());
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            var root = doc.RootElement;
            mod.ModId = GetStringProp(root, "id");
            mod.MetadataName = GetStringProp(root, "name");
            mod.MetadataVersion = NormalizeModVersion(GetStringProp(root, "version"));
            if (root.TryGetProperty("depends", out var dependencies)
                && dependencies.TryGetProperty("minecraft", out var minecraft))
            {
                mod.MetadataGameVersion = ExtractMinecraftVersionFromConstraint(minecraft);
            }
            mod.IdentifyMethod = ModIdentifyMethod.Metadata;
            return;
        }

        // Quilt
        var quiltEntry = zip.GetEntry("quilt.mod.json");
        if (quiltEntry != null)
        {
            using var reader = new StreamReader(quiltEntry.Open());
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            if (doc.RootElement.TryGetProperty("quilt_loader", out var loader))
            {
                mod.ModId = GetStringProp(loader, "id");
                mod.MetadataVersion = NormalizeModVersion(GetStringProp(loader, "version"));
                if (loader.TryGetProperty("metadata", out var meta))
                {
                    mod.MetadataName = GetStringProp(meta, "name");
                }
                if (loader.TryGetProperty("depends", out var dependencies)
                    && dependencies.TryGetProperty("minecraft", out var minecraft))
                {
                    mod.MetadataGameVersion = ExtractMinecraftVersionFromConstraint(minecraft);
                }
            }
            mod.IdentifyMethod = ModIdentifyMethod.Metadata;
            return;
        }

        // Forge / NeoForge（mods.toml 或 neoforge.mods.toml）
        var tomlEntry = zip.GetEntry("META-INF/mods.toml") ?? zip.GetEntry("META-INF/neoforge.mods.toml");
        if (tomlEntry != null)
        {
            using var reader = new StreamReader(tomlEntry.Open());
            var tomlText = reader.ReadToEnd();
            ParseModsToml(tomlText, mod);
            mod.IdentifyMethod = ModIdentifyMethod.Metadata;
            return;
        }

        // Bukkit / Spigot 插件
        var pluginEntry = zip.GetEntry("plugin.yml") ?? zip.GetEntry("paper-plugin.yml");
        if (pluginEntry != null)
        {
            using var reader = new StreamReader(pluginEntry.Open());
            var yaml = reader.ReadToEnd();
            mod.ModId = GetYamlValue(yaml, "name");
            mod.MetadataName = GetYamlValue(yaml, "name");
            mod.MetadataVersion = GetYamlValue(yaml, "version");
            mod.MetadataGameVersion = NormalizeGameVersionRange(GetYamlValue(yaml, "api-version"));
            mod.IdentifyMethod = ModIdentifyMethod.Metadata;
        }
        // 远古 Forge（mcmod.info）
        var legacyEntry = zip.GetEntry("mcmod.info");
        if (legacyEntry != null)
        {
            using var reader = new StreamReader(legacyEntry.Open());
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            var first = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                ? doc.RootElement[0]
                : doc.RootElement;
            mod.ModId = GetStringProp(first, "modid");
            mod.MetadataName = GetStringProp(first, "name");
            mod.MetadataVersion = NormalizeModVersion(GetStringProp(first, "version"));
            mod.MetadataGameVersion = NormalizeGameVersionRange(
                GetStringProp(first, "mcversion")
                ?? GetStringProp(first, "mcVersion")
                ?? GetStringProp(first, "modMcVersion"));
            mod.IdentifyMethod = ModIdentifyMethod.Metadata;
        }
    }

    private static void ParseModsToml(string tomlText, InstalledMod mod)
    {
        try
        {
            var table = Tomlyn.Toml.ToModel(tomlText);
            if (table.TryGetValue("mods", out var modsObj)
                && modsObj is Tomlyn.Model.TomlTableArray modsArray && modsArray.Count > 0)
            {
                var first = modsArray[0];
                mod.ModId = GetTomlString(first, "modId");
                mod.MetadataName = GetTomlString(first, "displayName");
                mod.MetadataVersion = NormalizeModVersion(GetTomlString(first, "version"));
            }

            mod.MetadataGameVersion = FindMinecraftDependencyRange(table);
        }
        catch
        {
            // TOML 解析失败时保留 Manifest 等其他来源的结果。
        }
    }

    private static string? FindMinecraftDependencyRange(Tomlyn.Model.TomlTable table)
    {
        if (!table.TryGetValue("dependencies", out var dependencies)) return null;

        IEnumerable<Tomlyn.Model.TomlTable> EnumerateDependencies() => dependencies switch
        {
            Tomlyn.Model.TomlTableArray array => array,
            Tomlyn.Model.TomlTable dependencyTable => dependencyTable.Values
                .OfType<Tomlyn.Model.TomlTableArray>()
                .SelectMany(array => array),
            _ => Enumerable.Empty<Tomlyn.Model.TomlTable>()
        };

        foreach (var dependency in EnumerateDependencies())
        {
            if (GetTomlString(dependency, "modId")?.Equals("minecraft", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NormalizeGameVersionRange(GetTomlString(dependency, "versionRange")
                    ?? GetTomlString(dependency, "version"));
            }
        }

        return null;
    }

    private static void ParseManifest(ZipArchive zip, InstalledMod mod)
    {
        var entry = zip.GetEntry("META-INF/MANIFEST.MF");
        if (entry == null) return;
        using var reader = new StreamReader(entry.Open());
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in reader.ReadToEnd().Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            attributes[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        mod.ManifestVersion = NormalizeModVersion(FirstAttribute(attributes, "Implementation-Version", "Bundle-Version", "Specification-Version"));
        mod.ManifestGameVersion = NormalizeGameVersionRange(FirstAttribute(attributes,
            "Minecraft-Version", "Minecraft-Version-Range", "MinecraftVersion", "Target-Minecraft-Version"));
    }

    private static string? FirstAttribute(IReadOnlyDictionary<string, string> attributes, params string[] names) =>
        names.Select(name => attributes.TryGetValue(name, out var value) ? value : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeModVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Trim('"', '\'');
        if (cleaned.Contains("${", StringComparison.Ordinal) || cleaned.Contains("file.jarVersion", StringComparison.OrdinalIgnoreCase)) return null;
        cleaned = Regex.Replace(cleaned, @"(?i)^(?:version|ver)\s*[:=]\s*", string.Empty).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string? NormalizeGameVersionRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim().Trim('"', '\'');
        var matches = Regex.Matches(cleaned, @"(?<![\d.])(?:1\.\d+(?:\.\d+)?|\d{2}\.\d+)(?![\d.])")
            .Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (matches.Count == 0) return null;
        if (matches.Count == 1) return matches[0];
        var separator = cleaned.Contains("||", StringComparison.Ordinal) || cleaned.Contains(',', StringComparison.Ordinal) ? " / " : " - ";
        return string.Join(separator, matches);
    }

    private static string? ExtractMinecraftVersionFromConstraint(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ValueKind == JsonValueKind.Array
                ? string.Join(" ", value.EnumerateArray().Select(item => item.ToString()))
                : null;
        return NormalizeGameVersionRange(text);
    }

    private static string? GetYamlValue(string text, string key)
    {
        var match = Regex.Match(text, $"(?m)^\\s*{Regex.Escape(key)}\\s*:\\s*[\\\"']?([^\\r\\n#\\\"']+)");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? GetTomlString(Tomlyn.Model.TomlTable table, string key) =>
        table.TryGetValue(key, out var val) && val is string s && !string.IsNullOrWhiteSpace(s) ? s : null;

    private static string? GetStringProp(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

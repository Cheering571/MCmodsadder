using System.IO;
using System.IO.Compression;
using System.Text.Json;
using McModsAdder.Models;
using McModsAdder.Providers;

namespace McModsAdder.Services;

/// <summary>
/// 识别 mods 目录中已安装的 mod：
/// 阶段一 并行 sha1 + Modrinth 批量哈希匹配（精确）；
/// 阶段二 未命中 jar 解析内部元数据兜底展示。
/// </summary>
public class ModJarAnalyzer
{
    private readonly IModProvider _provider;

    public ModJarAnalyzer(IModProvider provider)
    {
        _provider = provider;
    }

    public async Task AnalyzeAsync(GameInstance instance, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var files = InstanceScanner.EnumerateModFiles(instance.ModsPath);
        var mods = files.Select(f => new InstalledMod
        {
            FileName = Path.GetFileName(f),
            FullPath = f
        }).ToList();

        // ---- 阶段一：并行哈希 ----
        var semaphore = new SemaphoreSlim(4);
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
                // 单个文件哈希失败不影响整体
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref done);
                progress?.Report(current * 100 / Math.Max(mods.Count, 1));
            }
        });
        await Task.WhenAll(hashTasks);

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
                var names = await _provider.GetProjectNamesAsync(projectIds, ct);

                foreach (var (hash, info) in matched)
                {
                    if (hashToMod.TryGetValue(hash, out var mod))
                    {
                        mod.ProjectId = info.ProjectId;
                        mod.MatchedVersionId = info.VersionId;
                        mod.MatchedVersionNumber = info.VersionNumber;
                        mod.IdentifyMethod = ModIdentifyMethod.Hash;
                        if (names.TryGetValue(info.ProjectId, out var projectName))
                        {
                            mod.ProjectName = projectName;
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
    }

    /// <summary>解析 jar 内各加载器的元数据文件，填充兜底信息</summary>
    public static void ParseJarMetadata(InstalledMod mod)
    {
        using var zip = ZipFile.OpenRead(mod.FullPath);

        // Fabric
        var fabricEntry = zip.GetEntry("fabric.mod.json");
        if (fabricEntry != null)
        {
            using var reader = new StreamReader(fabricEntry.Open());
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            var root = doc.RootElement;
            mod.ModId = GetStringProp(root, "id");
            mod.MetadataName = GetStringProp(root, "name");
            mod.MetadataVersion = GetStringProp(root, "version");
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
                mod.MetadataVersion = GetStringProp(loader, "version");
                if (loader.TryGetProperty("metadata", out var meta))
                {
                    mod.MetadataName = GetStringProp(meta, "name");
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
            mod.MetadataVersion = GetStringProp(first, "version");
            mod.IdentifyMethod = ModIdentifyMethod.Metadata;
        }
    }

    private static void ParseModsToml(string tomlText, InstalledMod mod)
    {
        try
        {
            var table = Tomlyn.Toml.ToModel(tomlText);
            if (table.TryGetValue("mods", out var modsObj) &&
                modsObj is Tomlyn.Model.TomlTableArray modsArray && modsArray.Count > 0)
            {
                var first = modsArray[0];
                mod.ModId = GetTomlString(first, "modId");
                mod.MetadataName = GetTomlString(first, "displayName");
                mod.MetadataVersion = GetTomlString(first, "version");
                // 清理 ${file.jarVersion} 之类的占位符
                if (mod.MetadataVersion?.Contains("${") == true)
                {
                    mod.MetadataVersion = null;
                }
            }
        }
        catch
        {
            // toml 解析失败保持空值
        }
    }

    private static string? GetTomlString(Tomlyn.Model.TomlTable table, string key) =>
        table.TryGetValue(key, out var val) && val is string s && !string.IsNullOrWhiteSpace(s) ? s : null;

    private static string? GetStringProp(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

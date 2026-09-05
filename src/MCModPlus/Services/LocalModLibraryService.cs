using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MCModPlus.Models;

namespace MCModPlus.Services;

public sealed class DuplicateLocalModException : InvalidOperationException
{
    public LocalMod ExistingMod { get; }

    public DuplicateLocalModException(LocalMod existingMod)
        : base($"本地库中已存在相同的 Mod：{existingMod.Name}")
    {
        ExistingMod = existingMod;
    }
}

/// <summary>管理应用数据目录中的本地 Mod 副本与索引。</summary>
public class LocalModLibraryService
{
    private static readonly string LibraryDir = Path.Combine(SettingsService.DataDir, "local-mods");
    private static readonly string FilesDir = Path.Combine(LibraryDir, "files");
    private static readonly string ThumbnailsDir = Path.Combine(LibraryDir, "thumbnails");
    private static readonly string IndexPath = Path.Combine(LibraryDir, "index.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<LocalMod> Mods { get; private set; } = new();

    public void Load()
    {
        try
        {
            Mods = File.Exists(IndexPath)
                ? JsonSerializer.Deserialize<List<LocalMod>>(File.ReadAllText(IndexPath)) ?? new List<LocalMod>()
                : new List<LocalMod>();
        }
        catch
        {
            Mods = new List<LocalMod>();
        }

        Mods = Mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.Id)
                          && !string.IsNullOrWhiteSpace(mod.StoredFileName)
                          && Path.GetFileName(mod.StoredFileName) == mod.StoredFileName
                          && File.Exists(GetStoredPath(mod)))
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var thumbnailsUpdated = false;
        foreach (var mod in Mods)
        {
            if (!File.Exists(mod.ThumbnailPath))
            {
                mod.ThumbnailPath = ExtractThumbnail(mod);
                thumbnailsUpdated = true;
            }
        }
        if (thumbnailsUpdated) Save();
    }

    public LocalMod Add(string sourcePath)
    {
        if (!File.Exists(sourcePath) || !string.Equals(Path.GetExtension(sourcePath), ".jar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("请选择有效的 .jar Mod 文件。");
        }

        var sha1 = ComputeSha1(sourcePath);
        var existing = Mods.FirstOrDefault(mod =>
            string.Equals(GetOrComputeSha1(mod), sha1, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            throw new DuplicateLocalModException(existing);
        }

        Directory.CreateDirectory(FilesDir);
        var mod = CreateFromFile(sourcePath);
        mod.StoredFileName = $"{mod.Id}{Path.GetExtension(sourcePath)}";
        mod.Sha1 = sha1;
        File.Copy(sourcePath, GetStoredPath(mod), overwrite: true);
        mod.ThumbnailPath = ExtractThumbnail(mod);
        Mods.Add(mod);
        Mods = Mods.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Save();
        return mod;
    }

    private static string ComputeSha1(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(stream));
    }

    private string GetOrComputeSha1(LocalMod mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.Sha1)) return mod.Sha1;
        var storedPath = GetStoredPath(mod);
        if (!File.Exists(storedPath)) return string.Empty;
        mod.Sha1 = ComputeSha1(storedPath);
        return mod.Sha1;
    }

    public void Save()
    {
        Directory.CreateDirectory(LibraryDir);
        File.WriteAllText(IndexPath, JsonSerializer.Serialize(Mods, JsonOptions));
    }

    public void Delete(LocalMod mod)
    {
        DeleteFileWithRetry(GetStoredPath(mod));
        DeleteFileWithRetry(mod.ThumbnailPath);
        Mods.RemoveAll(item => item.Id == mod.Id);
        Save();
    }

    public IReadOnlyList<LocalMod> DeleteMany(IEnumerable<LocalMod> mods)
    {
        var deleted = new List<LocalMod>();
        foreach (var mod in mods.ToList())
        {
            try
            {
                DeleteFileWithRetry(GetStoredPath(mod));
                DeleteFileWithRetry(mod.ThumbnailPath);
                Mods.RemoveAll(item => item.Id == mod.Id);
                deleted.Add(mod);
            }
            catch
            {
            }
        }

        Save();
        return deleted;
    }

    private static void DeleteFileWithRetry(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(150);
            }
        }
    }

    public LocalMod? GetById(string id) => Mods.FirstOrDefault(mod => mod.Id == id);

    public string GetStoredPath(LocalMod mod) => Path.Combine(FilesDir, mod.StoredFileName);

    private static LocalMod CreateFromFile(string sourcePath)
    {
        var installed = new InstalledMod { FileName = Path.GetFileName(sourcePath), FullPath = sourcePath };
        try
        {
            ModJarAnalyzer.ParseJarMetadata(installed);
        }
        catch
        {
            // 元数据不可读时保留未知信息。
        }

        var loader = DetectLoader(sourcePath);
        var mod = new LocalMod
        {
            Name = installed.DisplayName,
            FileName = Path.GetFileName(sourcePath),
            Version = string.IsNullOrWhiteSpace(installed.DisplayVersion) ? "未知" : installed.DisplayVersion,
            Loader = loader,
            GameVersion = string.IsNullOrWhiteSpace(installed.DisplayGameVersion) ? "未知" : installed.DisplayGameVersion
        };
        return mod;
    }

    private string ExtractThumbnail(LocalMod mod)
    {
        try
        {
            using var zip = ZipFile.OpenRead(GetStoredPath(mod));
            var iconEntry = FindIconEntry(zip);
            if (iconEntry == null) return string.Empty;
            Directory.CreateDirectory(ThumbnailsDir);
            var targetPath = Path.Combine(ThumbnailsDir, $"{mod.Id}.png");
            using var source = iconEntry.Open();
            using var target = File.Create(targetPath);
            source.CopyTo(target);
            return targetPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ZipArchiveEntry? FindIconEntry(ZipArchive zip)
    {
        var preferredNames = new[] { "icon.png", "icon_128x128.png", "icon_64x64.png", "icon_32x32.png" };
        foreach (var name in preferredNames)
        {
            var entry = zip.Entries.FirstOrDefault(item => string.Equals(item.FullName, name, StringComparison.OrdinalIgnoreCase));
            if (entry != null) return entry;
        }

        return zip.Entries
            .Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal)
                            && entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName.Count(character => character == '/'))
            .ThenBy(entry => entry.FullName.Length)
            .FirstOrDefault();
    }

    private static ModLoader DetectLoader(string filePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            if (zip.GetEntry("fabric.mod.json") != null) return ModLoader.Fabric;
            if (zip.GetEntry("quilt.mod.json") != null) return ModLoader.Quilt;
            if (zip.GetEntry("META-INF/neoforge.mods.toml") != null) return ModLoader.NeoForge;
            if (zip.GetEntry("META-INF/mods.toml") != null || zip.GetEntry("mcmod.info") != null) return ModLoader.Forge;
        }
        catch
        {
        }
        return ModLoader.Unknown;
    }

}

using System.IO;
using System.Text.Json;
using McModsAdder.Models;

namespace McModsAdder.Services;

/// <summary>
/// 配置表 CRUD 与导入导出。存储于 %APPDATA%/McModsAdder/profiles/*.json
/// </summary>
public class ProfileService
{
    private static readonly string ProfilesDir =
        Path.Combine(SettingsService.DataDir, "profiles");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public List<ModProfile> Profiles { get; private set; } = new();

    public void LoadAll()
    {
        Profiles = new List<ModProfile>();
        try
        {
            if (!Directory.Exists(ProfilesDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(ProfilesDir, "*.json"))
            {
                try
                {
                    var profile = JsonSerializer.Deserialize<ModProfile>(File.ReadAllText(file));
                    if (profile != null && !string.IsNullOrEmpty(profile.Id))
                    {
                        Profiles.Add(profile);
                    }
                }
                catch
                {
                    // 单个文件损坏不影响其他配置表
                }
            }
        }
        catch
        {
            // 目录访问失败时返回空列表
        }

        Profiles = Profiles.OrderByDescending(p => p.UpdatedAt).ToList();
    }

    public ModProfile Create(string name)
    {
        var profile = new ModProfile { Name = name };
        Profiles.Insert(0, profile);
        Save(profile);
        return profile;
    }

    public void Save(ModProfile profile)
    {
        profile.UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(ProfilesDir);
        File.WriteAllText(GetPath(profile.Id), JsonSerializer.Serialize(profile, JsonOpts));
    }

    public void Delete(ModProfile profile)
    {
        Profiles.Remove(profile);
        var path = GetPath(profile.Id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public ModProfile? GetById(string id) => Profiles.FirstOrDefault(p => p.Id == id);

    /// <summary>导出配置表到指定文件</summary>
    public void Export(ModProfile profile, string destPath)
    {
        var export = new ProfileExportFile
        {
            Format = "mcmodsadder-profile",
            Version = 1,
            Name = profile.Name,
            Entries = profile.Entries
        };
        File.WriteAllText(destPath, JsonSerializer.Serialize(export, JsonOpts));
    }

    /// <summary>从文件导入配置表（按 projectId 去重合并），失败返回 null</summary>
    public ModProfile? Import(string sourcePath)
    {
        try
        {
            var export = JsonSerializer.Deserialize<ProfileExportFile>(File.ReadAllText(sourcePath));
            if (export?.Entries == null || export.Format != "mcmodsadder-profile")
            {
                return null;
            }

            var profile = new ModProfile { Name = export.Name ?? "导入的配置表" };
            var seen = new HashSet<string>();
            foreach (var entry in export.Entries)
            {
                if (!string.IsNullOrEmpty(entry.ProjectId) && seen.Add(entry.ProjectId))
                {
                    profile.Entries.Add(entry);
                }
            }

            Profiles.Insert(0, profile);
            Save(profile);
            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static string GetPath(string id) => Path.Combine(ProfilesDir, id + ".json");

    private class ProfileExportFile
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public string? Name { get; set; }
        public List<ProfileEntry>? Entries { get; set; }
    }
}

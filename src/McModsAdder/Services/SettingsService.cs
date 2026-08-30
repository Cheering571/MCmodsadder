using System.IO;
using System.Text.Json;
using McModsAdder.Models;

namespace McModsAdder.Services;

public class SettingsService
{
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McModsAdder");

    private static readonly string SettingsPath = Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    /// <summary>手动添加过的扫描根目录（.minecraft 目录或实例目录）</summary>
    public List<string> ScanRoots { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var doc = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(SettingsPath));
                if (doc != null)
                {
                    Current = doc.Settings ?? new AppSettings();
                    ScanRoots = doc.ScanRoots ?? new List<string>();
                }
            }
        }
        catch
        {
            Current = new AppSettings();
            ScanRoots = new List<string>();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var doc = new SettingsFile { Settings = Current, ScanRoots = ScanRoots };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(doc, JsonOpts));
        }
        catch
        {
            // 设置保存失败不阻断应用
        }
    }

    public void AddScanRoot(string path)
    {
        if (!ScanRoots.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            ScanRoots.Add(path);
            Save();
        }
    }

    private class SettingsFile
    {
        public AppSettings? Settings { get; set; }
        public List<string>? ScanRoots { get; set; }
    }
}

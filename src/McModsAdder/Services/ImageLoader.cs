using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace MCModPlus.Services;

/// <summary>
/// mod 图标异步加载与内存缓存（失败返回 null，UI 显示占位图标）。
/// </summary>
public static class ImageLoader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();

    public static async Task<BitmapImage?> GetAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        if (Cache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url, ct);
            using var source = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = source;
            bmp.DecodePixelWidth = 96;
            bmp.EndInit();
            bmp.Freeze();
            Cache[url] = bmp;
            return bmp;
        }
        catch
        {
            Cache[url] = null;
            return null;
        }
    }
}

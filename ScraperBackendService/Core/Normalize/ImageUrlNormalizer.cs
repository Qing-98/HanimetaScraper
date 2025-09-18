using Microsoft.AspNetCore.Mvc.Routing;
using ScraperBackendService.Models;

namespace ScraperBackendService.Core.Normalize;

public static class ImageUrlNormalizer
{
    /// <summary>
    /// 确保 URL 以 .jpg 结尾（webp → jpg），避免 Jellyfin 不能识别。
    /// </summary>
    public static string EnsureJpg(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        if (url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) return url;

        if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var idx = url.LastIndexOf('.');
            if (idx > 0) return url[..idx] + ".jpg";
        }
        return url;
    }

    /// <summary>
    /// 从 src/srcset 中挑选一个合适的 jpg。
    /// </summary>
    public static string PickJpg(string? src, string? srcset, string baseUrl)
    {
        string TryPick(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var abs = UrlHelper.Abs(s, baseUrl);
            return EnsureJpg(abs);
        }

        var a = TryPick(src);
        if (!string.IsNullOrEmpty(a)) return a;

        foreach (var part in (srcset ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim().Split(' ')[0];
            var m2 = TryPick(p);
            if (!string.IsNullOrEmpty(m2)) return m2;
        }
        return "";
    }

    /// <summary>
    /// 向缩略图列表添加（自动去重、转 jpg）。
    /// </summary>
    public static void AddThumb(HanimeMetadata meta, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var fixedUrl = EnsureJpg(url);
        if (!meta.Thumbnails.Contains(fixedUrl, StringComparer.OrdinalIgnoreCase))
            meta.Thumbnails.Add(fixedUrl);
    }
}

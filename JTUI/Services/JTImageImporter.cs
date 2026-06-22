using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace JTUI.Services
{
    /// <summary>
    /// 图片导入服务:把拖放数据 / 剪贴板内容解析为图片并落地到目标目录。
    /// 不依赖任何 UI 控件,返回新生成的文件路径。
    /// </summary>
    public class JTImageImporter
    {
        private static readonly string[] ImageExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>目标目录:所有导入的图片都落到这里。</summary>
        public string TargetDirectory { get; }

        public JTImageImporter(string targetDirectory)
        {
            TargetDirectory = targetDirectory;
            Directory.CreateDirectory(targetDirectory);
        }

        // ========== 入口:拖放 ==========

        /// <summary>判断这次拖放是否包含可导入的图片来源(用于 DragOver 设置光标)。</summary>
        public static bool CanAccept(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop)) return true;
            if (data.GetDataPresent(DataFormats.Html)) return true;
            if (data.GetDataPresent(DataFormats.Bitmap)) return true;
            if (data.GetDataPresent(DataFormats.Dib)) return true;
            if (data.GetDataPresent(DataFormats.Text))
            {
                var t = data.GetData(DataFormats.Text) as string;
                return LooksLikeImageUrl(t);
            }
            return false;
        }

        /// <summary>解析一次拖放,返回所有成功导入的新文件路径。</summary>
        public async Task<IReadOnlyList<JTImportResult>> ImportFromDropAsync(
            IDataObject data, CancellationToken ct = default)
        {
            var results = new List<JTImportResult>();

            // 1) 本地文件
            if (data.GetDataPresent(DataFormats.FileDrop) &&
                data.GetData(DataFormats.FileDrop) is string[] files)
            {
                bool any = false;
                foreach (var f in files)
                {
                    any = true;
                    if (!IsImageFile(f))
                    {
                        results.Add(JTImportResult.Fail(
                            JTImportFailReason.UnsupportedFormat, Path.GetFileName(f),
                            $"不支持的格式: {Path.GetExtension(f)}"));
                        continue;
                    }
                    var dst = CopyLocal(f);
                    results.Add(dst != null
                        ? JTImportResult.Ok(dst, Path.GetFileName(f))
                        : JTImportResult.Fail(JTImportFailReason.CopyFailed, Path.GetFileName(f)));
                }
                if (any) return results;   // 本地文件命中即返回(含失败记录)
            }

            // 2) 浏览器 HTML <img src>
            if (data.GetDataPresent(DataFormats.Html) &&
                data.GetData(DataFormats.Html) is string html)
            {
                var url = ExtractImageUrlFromHtml(html);
                if (url != null)
                {
                    var dst = await DownloadAsync(url, ct);
                    results.Add(dst != null
                        ? JTImportResult.Ok(dst, url)
                        : JTImportResult.Fail(JTImportFailReason.DownloadFailed, url));
                    return results;
                }
            }

            // 3) 纯文本 URL
            if (data.GetDataPresent(DataFormats.Text) &&
                data.GetData(DataFormats.Text) is string text)
            {
                if (LooksLikeImageUrl(text))
                {
                    var dst = await DownloadAsync(text.Trim(), ct);
                    results.Add(dst != null
                        ? JTImportResult.Ok(dst, text.Trim())
                        : JTImportResult.Fail(JTImportFailReason.DownloadFailed, text.Trim()));
                }
                else
                {
                    results.Add(JTImportResult.Fail(
                        JTImportFailReason.UnsupportedFormat, text.Trim(), "不是有效的图片链接"));
                }
                return results;
            }

            // 4) 裸位图流
            if (data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent(DataFormats.Dib))
            {
                var src = data.GetData(DataFormats.Bitmap) as BitmapSource;
                var dst = src != null ? SaveBitmapSource(src) : null;
                results.Add(dst != null
                    ? JTImportResult.Ok(dst, "拖放位图")
                    : JTImportResult.Fail(JTImportFailReason.DecodeFailed, "拖放位图"));
                return results;
            }

            // 啥都没匹配上
            results.Add(JTImportResult.Fail(JTImportFailReason.NoImageInData, "拖放数据"));
            return results;
        }

        // ========== 入口:剪贴板 ==========

        /// <summary>从剪贴板粘贴图片(剪贴板单算,直接调用)。返回新文件路径或 null。</summary>
        public JTImportResult ImportFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    foreach (var f in Clipboard.GetFileDropList().Cast<string>())
                    {
                        if (!IsImageFile(f))
                            return JTImportResult.Fail(JTImportFailReason.UnsupportedFormat, Path.GetFileName(f));
                        var dst = CopyLocal(f);
                        if (dst != null) return JTImportResult.Ok(dst, Path.GetFileName(f));
                    }
                }
                if (Clipboard.ContainsImage())
                {
                    var src = Clipboard.GetImage();
                    var dst = src != null ? SaveBitmapSource(src) : null;
                    return dst != null
                        ? JTImportResult.Ok(dst, "剪贴板")
                        : JTImportResult.Fail(JTImportFailReason.DecodeFailed, "剪贴板");
                }
                return JTImportResult.Fail(JTImportFailReason.NoImageInData, "剪贴板");
            }
            catch (Exception ex)
            {
                return JTImportResult.Fail(JTImportFailReason.Unknown, "剪贴板", ex.Message);
            }
        }


        // ========== 落地实现 ==========

        private string? CopyLocal(string sourcePath)
        {
            try
            {
                string dst = UniquePath(Path.GetExtension(sourcePath));
                File.Copy(sourcePath, dst, overwrite: false);
                return dst;
            }
            catch { return null; }
        }

        private async Task<string?> DownloadAsync(string url, CancellationToken ct)
        {
            try
            {
                using var resp = await Http.GetAsync(url, ct);
                resp.EnsureSuccessStatusCode();
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct);

                // 用 SkiaSharp 嗅探真实格式,避免 URL 里没有/伪造扩展名
                string ext = SniffExtension(bytes) ?? GuessExtFromUrl(url) ?? ".png";
                string dst = UniquePath(ext);
                await File.WriteAllBytesAsync(dst, bytes, ct);
                return dst;
            }
            catch { return null; }
        }

        private string? SaveBitmapSource(BitmapSource src)
        {
            try
            {
                string dst = UniquePath(".png");
                using var fs = new FileStream(dst, FileMode.Create);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(src));
                encoder.Save(fs);
                return dst;
            }
            catch { return null; }
        }

        // ========== 工具 ==========

        private string UniquePath(string ext)
        {
            if (string.IsNullOrEmpty(ext) || ext[0] != '.') ext = "." + ext;
            string name = $"img_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}".Substring(0, 30);
            return Path.Combine(TargetDirectory, name + ext.ToLowerInvariant());
        }

        private static bool IsImageFile(string path) =>
            ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant());

        private static bool LooksLikeImageUrl(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return false;
            if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) return false;
            // 有图片扩展名,或干脆放行 http(s)(很多 CDN 图无扩展名)
            return true;
        }

        private static string? GuessExtFromUrl(string url)
        {
            try
            {
                string ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
                return ImageExts.Contains(ext) ? ext : null;
            }
            catch { return null; }
        }

        /// <summary>从 CF_HTML 片段里抠出第一个 &lt;img src&gt; 的 URL。</summary>
        private static string? ExtractImageUrlFromHtml(string html)
        {
            var m = Regex.Match(html, @"<img[^>]+src\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            string url = m.Groups[1].Value;
            if (url.StartsWith("//")) url = "https:" + url;
            return LooksLikeImageUrl(url) ? url : null;
        }

        /// <summary>用 SkiaSharp 解码头判断真实格式(下载/剪贴板字节流用)。</summary>
        private static string? SniffExtension(byte[] bytes)
        {
            try
            {
                using var codec = SKCodec.Create(new MemoryStream(bytes));
                if (codec == null) return null;
                return codec.EncodedFormat switch
                {
                    SKEncodedImageFormat.Png => ".png",
                    SKEncodedImageFormat.Jpeg => ".jpg",
                    SKEncodedImageFormat.Gif => ".gif",
                    SKEncodedImageFormat.Bmp => ".bmp",
                    SKEncodedImageFormat.Webp => ".webp",
                    _ => null
                };
            }
            catch { return null; }
        }
    }
}

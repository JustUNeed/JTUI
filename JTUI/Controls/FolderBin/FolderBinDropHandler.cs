using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;

namespace JTUI.Controls.FolderBin
{
    internal sealed class FolderBinDropHandler : IDropTarget
    {
        private readonly JTFolderBin _bin;
        public FolderBinDropHandler(JTFolderBin bin) => _bin = bin;

        public void DragOver(IDropInfo dropInfo)
        {
            ClearActive();

            // A) 列表内重排:拖的是 JTFolderItem
            if (IsReorder(dropInfo))
            {
                if (!_bin.AllowReorderInternal) { dropInfo.Effects = DragDropEffects.None; return; }
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
                return;
            }

            // 外部数据(文件 / 浏览器内容)
            if (dropInfo.Data is not IDataObject data) { dropInfo.Effects = DragDropEffects.None; return; }

            // B) 落点正好压在某个格子上 → 收文件 / 下载
            if (dropInfo.TargetItem is JTFolderItem folder)
            {
                bool hasFiles = data.GetDataPresent(DataFormats.FileDrop);
                bool hasUrl = TryGetUrl(data, out _);
                if (hasFiles || hasUrl)
                {
                    folder.IsDropActive = true;                    // 高亮该格子
                    dropInfo.Effects = hasFiles ? DragDropEffects.Move : DragDropEffects.Copy;
                    return;
                }
            }

            // C) 落在空白/格子之间,且拖的是文件夹 → 新增格子
            if (data.GetDataPresent(DataFormats.FileDrop) &&
                data.GetData(DataFormats.FileDrop) is string[] paths &&
                paths.Any(System.IO.Directory.Exists))
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Copy;
                return;
            }

            dropInfo.Effects = DragDropEffects.None;
        }

        public async void Drop(IDropInfo dropInfo)
        {
            ClearActive();

            // A) 重排
            if (IsReorder(dropInfo))
            {
                if (!_bin.AllowReorderInternal) return;
                Reorder(dropInfo);
                return;
            }

            if (dropInfo.Data is not IDataObject data) return;

            // B) 投放到某格子
            if (dropInfo.TargetItem is JTFolderItem folder)
            {
                if (data.GetDataPresent(DataFormats.FileDrop) &&
                    data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    await _bin.MoveFilesIntoAsync(folder, files);
                    return;
                }
                if (TryGetUrl(data, out string url))
                {
                    await _bin.DownloadUrlIntoAsync(folder, url);
                    return;
                }
            }

            // C) 新增文件夹格子
            if (data.GetDataPresent(DataFormats.FileDrop) &&
                data.GetData(DataFormats.FileDrop) is string[] dropPaths)
            {
                var folders = dropPaths.Where(System.IO.Directory.Exists).ToArray();
                if (folders.Length > 0) _bin.AddFolders(folders);
            }
        }

        // ---------- 工具 ----------

        private static bool IsReorder(IDropInfo dropInfo) =>
            dropInfo.Data is JTFolderItem ||
            (dropInfo.Data is System.Collections.IEnumerable e && dropInfo.Data is not string &&
             e.Cast<object>().Any() && e.Cast<object>().All(o => o is JTFolderItem));

        private void Reorder(IDropInfo dropInfo)
        {
            var col = _bin.Items_;
            var dragged = (dropInfo.Data is System.Collections.IEnumerable en && dropInfo.Data is not string)
                ? en.Cast<object>().OfType<JTFolderItem>().ToList()
                : new List<JTFolderItem>();
            if (dragged.Count == 0 && dropInfo.Data is JTFolderItem one) dragged.Add(one);
            if (dragged.Count == 0) return;

            int insert = dropInfo.InsertIndex;
            foreach (var item in dragged)
            {
                int old = col.IndexOf(item);
                if (old < 0) continue;
                col.RemoveAt(old);
                if (old < insert) insert--;
            }
            if (insert < 0) insert = 0;
            if (insert > col.Count) insert = col.Count;
            foreach (var item in dragged) col.Insert(insert++, item);

            _bin.RaiseListChanged();
        }

        // 从浏览器拖入数据里提取 URL
        private static bool TryGetUrl(IDataObject data, out string url)
        {
            url = "";
            // 1) 标准 URL 格式(IE/Edge/Chrome 多会带)
            foreach (var fmt in new[] { "UniformResourceLocatorW", "UniformResourceLocator" })
            {
                if (data.GetDataPresent(fmt))
                {
                    var s = ReadString(data, fmt);
                    if (IsHttp(s)) { url = s!.Trim(); return true; }
                }
            }
            // 2) 纯文本里是个链接
            if (data.GetDataPresent(DataFormats.Text))
            {
                var s = data.GetData(DataFormats.Text) as string;
                if (IsHttp(s)) { url = s!.Trim(); return true; }
            }
            // 3) 从 HTML 片段里抠 <img src> / 第一个 http 链接
            if (data.GetDataPresent(DataFormats.Html))
            {
                var html = data.GetData(DataFormats.Html) as string;
                var m = System.Text.RegularExpressions.Regex.Match(
                    html ?? "", @"https?://[^\s""'<>]+");
                if (m.Success) { url = m.Value; return true; }
            }
            return false;
        }

        private static string? ReadString(IDataObject data, string fmt)
        {
            var o = data.GetData(fmt);
            if (o is string s) return s;
            if (o is System.IO.MemoryStream ms)
            {
                var bytes = ms.ToArray();
                // UniformResourceLocatorW 是 Unicode
                return fmt.EndsWith("W")
                    ? System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0')
                    : System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
            }
            return null;
        }

        private static bool IsHttp(string? s) =>
            !string.IsNullOrWhiteSpace(s) &&
            (s!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        private void ClearActive()
        {
            foreach (var i in _bin.Items_) i.IsDropActive = false;
        }
    }
}

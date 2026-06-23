using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GongDragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace JTUI.Controls.FolderBin
{
    /// <summary>
    /// 文件夹收纳格:每个格子代表一个文件夹(色块 + 名称)。
    /// - 往列表空白处拖入文件夹 → 新增格子;
    /// - 往某个格子拖入文件 → 移动该文件到对应文件夹;
    /// - 往某个格子拖入浏览器内容(图片/链接)→ 下载到对应文件夹;
    /// - 支持拖拽排序,排序/增删触发 ListChanged 供外部持久化。
    /// 不显示缩略图,只显示方形色块。
    /// </summary>
    public class JTFolderBin : ListBox
    {
        private readonly ObservableCollection<JTFolderItem> _items = new();
        private readonly FolderBinDropHandler _dropHandler;

        // 默认下载器(可被 DownloadHandler 覆盖)
        private static readonly HttpClient _http = new();

        static JTFolderBin()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTFolderBin), new FrameworkPropertyMetadata(typeof(JTFolderBin)));
        }

        public JTFolderBin()
        {
            ItemsSource = _items;
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
            new RoutedEventHandler(OnDeleteButtonClick));   // ← 加这行

            _dropHandler = new FolderBinDropHandler(this);

            GongDragDrop.SetIsDragSource(this, true);
            GongDragDrop.SetIsDropTarget(this, true);
            GongDragDrop.SetDropHandler(this, _dropHandler);
        }

        // ---------- 对外事件 ----------

        /// <summary>列表内容(文件夹格子)变化:新增 / 删除 / 排序。参数为路径快照,供持久化。</summary>
        public event Action<IReadOnlyList<string>>? ListChanged;

        /// <summary>往某格子投放完成(移动文件 / 下载链接 / 失败)。</summary>
        public event Action<JTFolderDropResult>? Dropped;

        /// <summary>格子左键单击,参数为文件夹路径。</summary>
        public event Action<string>? ItemClicked;

        /// <summary>格子右键单击,参数为文件夹路径。</summary>
        public event Action<string>? ItemRightClick;

        internal void RaiseListChanged() => ListChanged?.Invoke(GetPaths());
        internal void RaiseDropped(JTFolderDropResult r) => Dropped?.Invoke(r);

        // ---------- 可覆盖的下载逻辑 ----------

        /// <summary>
        /// 自定义下载委托:输入(URL, 目标文件夹),返回落地文件路径(失败返回 null)。
        /// 为 null 时用内置 HttpClient 简单下载。可换成自带进度/鉴权的实现。
        /// </summary>
        public Func<string, string, Task<string?>>? DownloadHandler { get; set; }

        // ---------- 依赖属性 ----------

 
   
        /// <summary>格子宽度(像素),默认 160。</summary>
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register(nameof(ItemWidth), typeof(double),
                typeof(JTFolderBin), new FrameworkPropertyMetadata(160.0));
        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        /// <summary>格子高度(像素),默认 44。</summary>
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(JTFolderBin), new FrameworkPropertyMetadata(44.0));
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }




        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JTFolderBin), new FrameworkPropertyMetadata(6.0));
        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public static readonly DependencyProperty ShowDeleteButtonProperty =
            DependencyProperty.Register(nameof(ShowDeleteButton), typeof(bool),
                typeof(JTFolderBin), new FrameworkPropertyMetadata(false));
        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }

        public static readonly DependencyProperty AllowReorderProperty =
            DependencyProperty.Register(nameof(AllowReorder), typeof(bool),
                typeof(JTFolderBin), new FrameworkPropertyMetadata(true));
        public bool AllowReorder
        {
            get => (bool)GetValue(AllowReorderProperty);
            set => SetValue(AllowReorderProperty, value);
        }

        public static readonly DependencyProperty DistinctProperty =
            DependencyProperty.Register(nameof(Distinct), typeof(bool),
                typeof(JTFolderBin), new FrameworkPropertyMetadata(true));
        public bool Distinct
        {
            get => (bool)GetValue(DistinctProperty);
            set => SetValue(DistinctProperty, value);
        }

        internal ObservableCollection<JTFolderItem> Items_ => _items;
        internal bool AllowReorderInternal => AllowReorder;
        internal bool DistinctInternal => Distinct;

        // ---------- 外部接口 ----------

        /// <summary>【初始化/恢复】用一批文件夹路径替换整个列表。不触发 ListChanged。</summary>
        public void SetFolders(IEnumerable<string> paths)
        {
            _items.Clear();
            if (paths == null) return;
            foreach (var p in paths) AddFolderSilent(p);
        }

        /// <summary>【外部添加文件夹】成功返回 true 并触发 ListChanged。</summary>
        public bool AddFolder(string path)
        {
            if (!AddFolderSilent(path)) return false;
            RaiseListChanged();
            return true;
        }

        /// <summary>【批量添加】只在末尾触发一次 ListChanged。</summary>
        public void AddFolders(IEnumerable<string> paths)
        {
            if (paths == null) return;
            bool any = false;
            foreach (var p in paths) if (AddFolderSilent(p)) any = true;
            if (any) RaiseListChanged();
        }

        public bool RemoveFolder(string path)
        {
            var vm = _items.FirstOrDefault(i =>
                string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
            if (vm == null) return false;
            _items.Remove(vm);
            RaiseListChanged();
            return true;
        }

        public void Clear()
        {
            if (_items.Count == 0) return;
            _items.Clear();
            RaiseListChanged();
        }

        public IReadOnlyList<string> GetPaths() => _items.Select(i => i.Path).ToList();

        // 只接受真实存在的文件夹
        internal bool AddFolderSilent(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path)) return false;            // 只允许文件夹
            if (Distinct && _items.Any(i =>
                    string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)))
                return false;
            _items.Add(new JTFolderItem(path));
            return true;
        }

        // ---------- 投放执行:移动文件 / 下载链接 ----------

        /// <summary>把一批本地文件移动进目标文件夹(同盘 Move / 跨盘 Copy+Delete)。</summary>
        internal async Task MoveFilesIntoAsync(JTFolderItem folder, IEnumerable<string> files)
        {
            foreach (var src in files)
            {
                try
                {
                    if (Directory.Exists(src))   // 拖进来的是文件夹则跳过(本格子只收文件)
                    {
                        RaiseDropped(new JTFolderDropResult
                        {
                            Kind = JTFolderDropKind.Failed,
                            FolderPath = folder.Path,
                            Source = src,
                            Error = "不支持把文件夹放入格子"
                        });
                        continue;
                    }
                    if (!File.Exists(src)) continue;

                    string dest = UniqueDestPath(folder.Path, Path.GetFileName(src));
                    await Task.Run(() => MoveAcrossVolumes(src, dest));

                    RaiseDropped(new JTFolderDropResult
                    {
                        Kind = JTFolderDropKind.MovedFile,
                        FolderPath = folder.Path,
                        Source = src,
                        ResultPath = dest
                    });
                }
                catch (Exception ex)
                {
                    RaiseDropped(new JTFolderDropResult
                    {
                        Kind = JTFolderDropKind.Failed,
                        FolderPath = folder.Path,
                        Source = src,
                        Error = ex.Message
                    });
                }
            }
        }

        /// <summary>把一个 URL 下载进目标文件夹。</summary>
        internal async Task DownloadUrlIntoAsync(JTFolderItem folder, string url)
        {
            try
            {
                string? saved = DownloadHandler != null
                    ? await DownloadHandler(url, folder.Path)
                    : await DefaultDownloadAsync(url, folder.Path);

                RaiseDropped(saved != null
                    ? new JTFolderDropResult
                    {
                        Kind = JTFolderDropKind.DownloadedUrl,
                        FolderPath = folder.Path,
                        Source = url,
                        ResultPath = saved
                    }
                    : new JTFolderDropResult
                    {
                        Kind = JTFolderDropKind.Failed,
                        FolderPath = folder.Path,
                        Source = url,
                        Error = "下载失败"
                    });
            }
            catch (Exception ex)
            {
                RaiseDropped(new JTFolderDropResult
                {
                    Kind = JTFolderDropKind.Failed,
                    FolderPath = folder.Path,
                    Source = url,
                    Error = ex.Message
                });
            }
        }

        private static async Task<string?> DefaultDownloadAsync(string url, string folder)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            string name = GuessFileName(url, resp);
            string dest = UniqueDestPath(folder, name);

            await using var fs = File.Create(dest);
            await resp.Content.CopyToAsync(fs);
            return dest;
        }

        private static string GuessFileName(string url, HttpResponseMessage resp)
        {
            // 优先用响应头里的文件名
            var cd = resp.Content.Headers.ContentDisposition;
            if (!string.IsNullOrEmpty(cd?.FileNameStar)) return Sanitize(cd!.FileNameStar!);
            if (!string.IsNullOrEmpty(cd?.FileName)) return Sanitize(cd!.FileName!.Trim('"'));

            try
            {
                var uri = new Uri(url);
                string n = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(n)) return Sanitize(n);
            }
            catch { }

            // 没文件名就按 MIME 猜扩展名
            string ext = resp.Content.Headers.ContentType?.MediaType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                _ => ".bin"
            };
            return $"download_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        // 目标已存在则自动追加 (1)(2)… 避免覆盖
        private static string UniqueDestPath(string folder, string fileName)
        {
            string dest = Path.Combine(folder, fileName);
            if (!File.Exists(dest)) return dest;

            string stem = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int i = 1;
            do { dest = Path.Combine(folder, $"{stem} ({i++}){ext}"); }
            while (File.Exists(dest));
            return dest;
        }

        private static void MoveAcrossVolumes(string src, string dest)
        {
            // 同盘直接 Move;跨盘 Move 会抛 IOException,降级为 Copy + Delete
            if (string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dest),
                              StringComparison.OrdinalIgnoreCase))
            {
                File.Move(src, dest);
            }
            else
            {
                File.Copy(src, dest, overwrite: false);
                File.Delete(src);
            }
        }

        // ---------- 点击 / 右键 ----------

        protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            if (IsInDeleteButton(e.OriginalSource)) return;
            if (GetItemFromEvent(e.OriginalSource) is JTFolderItem vm)
                ItemClicked?.Invoke(vm.Path);
        }

        protected override void OnPreviewMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);
            if (GetItemFromEvent(e.OriginalSource) is JTFolderItem vm)
                ItemRightClick?.Invoke(vm.Path);
        }

        // 删除按钮(模板里 Button.Tag = JTFolderItem)
        public JTFolderBin AttachDeleteHandler()
        {
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnDeleteButtonClick));
            return this;
        }

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is JTFolderItem vm)
            {
                _items.Remove(vm);
                RaiseListChanged();
                e.Handled = true;
            }
        }

        internal static JTFolderItem? GetItemFromEvent(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem item && item.DataContext is JTFolderItem vm) return vm;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        private static bool IsInDeleteButton(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is Button btn && btn.Tag is JTFolderItem) return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }
    }
}

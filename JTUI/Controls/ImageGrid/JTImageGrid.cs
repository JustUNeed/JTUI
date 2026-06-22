using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace JTUI.Controls.ImageGrid
{
    /// <summary>
    /// 极简图片瀑布流:设置 ImageDirectory,虚拟化展示目录里所有图片。
    /// 缩略图后台线程当场解码(按 ThumbnailSize 缩小),不缓存、不下载。
    /// 容器进入可视区才解码,移出即取消,滚动流畅。
    /// </summary>
    public class JTImageGrid : ListBox
    {
        private static readonly string[] ImageExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        private readonly ObservableCollection<JTImageItem> _items = new();
        private readonly Dictionary<JTImageItem, CancellationTokenSource> _loading = new();
        private readonly SemaphoreSlim _gate = new(Environment.ProcessorCount);

        static JTImageGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTImageGrid),
                new FrameworkPropertyMetadata(typeof(JTImageGrid)));

        }

        public JTImageGrid() {
            ItemsSource = _items;

            // 监听模板内删除按钮的点击(用附加事件,避免逐项绑定)
            AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                new RoutedEventHandler(OnDeleteButtonClick));

        }

        /// <summary>图库目录:展示其中所有图片。</summary>
        public static readonly DependencyProperty ImageDirectoryProperty =
            DependencyProperty.Register(nameof(ImageDirectory), typeof(string),
                typeof(JTImageGrid),
                new FrameworkPropertyMetadata(null, OnImageDirectoryChanged));

        public string? ImageDirectory
        {
            get => (string?)GetValue(ImageDirectoryProperty);
            set => SetValue(ImageDirectoryProperty, value);
        }

        /// <summary>瀑布流列宽(像素),默认 180。容器变宽时按此宽度自动增列、换行填充。</summary>
        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(100.0));

        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        /// <summary>缩略图解码尺寸(像素),默认 360。</summary>
        public static readonly DependencyProperty ThumbnailSizeProperty =
            DependencyProperty.Register(nameof(ThumbnailSize), typeof(int),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(360));

        public int ThumbnailSize
        {
            get => (int)GetValue(ThumbnailSizeProperty);
            set => SetValue(ThumbnailSizeProperty, value);
        }

        private static void OnImageDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((JTImageGrid)d).Reload();

        /// <summary>重新扫描目录加载图片(外部下载完图片后可调用此方法刷新)。</summary>
  

        private static bool IsImageFile(string path) =>
            ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant());

        // ---------- 容器进入可视区→解码,移出→取消 ----------





        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            System.Diagnostics.Debug.WriteLine($"[Prepare] item={item is JTImageItem}, path={(item as JTImageItem)?.Path}");
            if (item is JTImageItem vm && vm.Thumbnail == null)
                _ = LoadAsync(vm);
        }


        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            if (item is JTImageItem vm && _loading.TryGetValue(vm, out var cts))
            {
                try { cts.Cancel(); } catch { /* 已 dispose 则忽略 */ }
                // 不在这里 Dispose,交给 LoadAsync 的 finally 统一释放
            }
        }

        private async Task LoadAsync(JTImageItem vm)
        {
            if (_loading.ContainsKey(vm)) return;
            var cts = new CancellationTokenSource();
            _loading[vm] = cts;
            int decodeSize = ThumbnailSize;
            try
            {
                await _gate.WaitAsync(cts.Token);
                try
                {
                    var bmp = await Task.Run(() => Decode(vm.Path, decodeSize), cts.Token);
                    if (!cts.Token.IsCancellationRequested && bmp != null)
                        vm.Thumbnail = bmp;
                }
                finally { _gate.Release(); }
            }
            catch (OperationCanceledException) { /* 滚走了,正常取消 */ }
            catch { /* 解码失败,保持空白占位 */ }
            finally
            {
                if (_loading.TryGetValue(vm, out var c) && c == cts)
                    _loading.Remove(vm);
                cts.Dispose();   // 唯一的 Dispose 点
            }
        }


        public void Reload()
        {
            _items.Clear();
            string? dir = ImageDirectory;
            System.Diagnostics.Debug.WriteLine($"[Reload] dir={dir}, exists={Directory.Exists(dir ?? "")}");
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

            foreach (var file in Directory.EnumerateFiles(dir)
                         .Where(IsImageFile).OrderBy(f => f))
                _items.Add(new JTImageItem(file));
            System.Diagnostics.Debug.WriteLine($"[Reload] loaded {_items.Count} items");
        }


        private static BitmapSource? Decode(string path, int decodePixelWidth)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;            // 读完即释放文件句柄
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.DecodePixelWidth = decodePixelWidth;               // 解码时直接缩小
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();                                          // 跨线程 + 渲染优化
                return bmp;
            }
            catch { return null; }
        }


        // ---------- 新增依赖属性 ----------

        /// <summary>图片之间的间隔(像素),默认 6。</summary>
        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(6.0));

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        /// <summary>是否允许悬浮显示右上角删除按钮,默认 false。</summary>
        public static readonly DependencyProperty ShowDeleteButtonProperty =
            DependencyProperty.Register(nameof(ShowDeleteButton), typeof(bool),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(false));

        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }


        // ---------- 路由事件 ----------

        public static readonly RoutedEvent ImageLeftClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ImageLeftClick),
                RoutingStrategy.Bubble, typeof(EventHandler<JTImageEventArgs>), typeof(JTImageGrid));

        public event EventHandler<JTImageEventArgs> ImageLeftClick
        {
            add => AddHandler(ImageLeftClickEvent, value);
            remove => RemoveHandler(ImageLeftClickEvent, value);
        }

        public static readonly RoutedEvent ImageRightClickEvent =
            EventManager.RegisterRoutedEvent(nameof(ImageRightClick),
                RoutingStrategy.Bubble, typeof(EventHandler<JTImageEventArgs>), typeof(JTImageGrid));

        public event EventHandler<JTImageEventArgs> ImageRightClick
        {
            add => AddHandler(ImageRightClickEvent, value);
            remove => RemoveHandler(ImageRightClickEvent, value);
        }

        public static readonly RoutedEvent ImageDeleteEvent =
            EventManager.RegisterRoutedEvent(nameof(ImageDelete),
                RoutingStrategy.Bubble, typeof(EventHandler<JTImageEventArgs>), typeof(JTImageGrid));

        public event EventHandler<JTImageEventArgs> ImageDelete
        {
            add => AddHandler(ImageDeleteEvent, value);
            remove => RemoveHandler(ImageDeleteEvent, value);
        }

        // ---------- 点击与删除处理 ----------

        protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            if (GetItemFromEvent(e.OriginalSource) is JTImageItem vm)
                RaiseEvent(new JTImageEventArgs(ImageLeftClickEvent, this, vm.Path));
        }

        protected override void OnPreviewMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);
            if (GetItemFromEvent(e.OriginalSource) is JTImageItem vm)
                RaiseEvent(new JTImageEventArgs(ImageRightClickEvent, this, vm.Path));
        }

        private JTImageItem? GetItemFromEvent(object originalSource)
        {
            var dep = originalSource as System.Windows.DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem item && item.DataContext is JTImageItem vm)
                    return vm;
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        /// <summary>删除按钮点击(模板里的按钮通过 Tag 传入对应项)。</summary>
        internal void RequestDelete(JTImageItem vm)
        {
            RaiseEvent(new JTImageEventArgs(ImageDeleteEvent, this, vm.Path));
            _items.Remove(vm);   // 从列表移除;是否删文件由外部事件决定
        }


        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe &&
                fe.TemplatedParent is System.Windows.Controls.Button btn &&
                btn.Tag is JTImageItem vm)
            {
                RequestDelete(vm);
                e.Handled = true;
            }
            else if (e.OriginalSource is System.Windows.Controls.Button b && b.Tag is JTImageItem vm2)
            {
                RequestDelete(vm2);
                e.Handled = true;
            }
        }
    }
}

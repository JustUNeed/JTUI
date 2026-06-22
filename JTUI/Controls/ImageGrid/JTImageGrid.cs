using JTUI.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JTUI.Controls.ImageGrid
{
    /// <summary>
    /// 极简图片瀑布流:设置 ImageDirectory 即虚拟化展示目录里所有图片。
    /// 缩略图后台线程当场解码(按 ThumbnailSize 缩小),不缓存、不下载。
    /// 容器进入可视区才解码,移出即取消。
    /// </summary>
    public class JTImageGrid : ListBox
    {
        private static readonly string[] ImageExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        private readonly ObservableCollection<JTImageItem> _items = new();
        private readonly Dictionary<JTImageItem, CancellationTokenSource> _loading = new();
        private readonly SemaphoreSlim _gate = new(Environment.ProcessorCount);

        private ScrollViewer? _scrollViewer;

        // 往外拖(drag source)
        private Point _dragStart;
        private JTImageItem? _dragCandidate;
        private bool _outDragging;
        private const double OutDragThreshold = 6.0;


        static JTImageGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTImageGrid), new FrameworkPropertyMetadata(typeof(JTImageGrid)));
        }

        public JTImageGrid()
        {
            ItemsSource = _items;
            AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnDeleteButtonClick));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        }

        // ---------- 回调(对外通知) ----------

        /// <summary>图片左键单击,参数为图片完整路径。</summary>
        public event Action<string>? ImageLeftClick;

        /// <summary>图片右键单击,参数为图片完整路径。</summary>
        public event Action<string>? ImageRightClick;

        /// <summary>图片被删除(已从列表移除),参数为图片完整路径。</summary>
        public event Action<string>? ImageDeleted;

        /// <summary>拖入成功添加一张图片,参数为新文件路径。</summary>
        public event Action<string>? ImageImported;

        /// <summary>拖入失败,参数为(失败原因, 来源描述)。</summary>
        public event Action<JTImportFailReason, string>? ImportFailed;

        // ---------- 依赖属性 ----------

        /// <summary>图库目录:展示其中所有图片。</summary>
        public static readonly DependencyProperty ImageDirectoryProperty =
            DependencyProperty.Register(nameof(ImageDirectory), typeof(string),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(null, OnImageDirectoryChanged));

        public string? ImageDirectory
        {
            get => (string?)GetValue(ImageDirectoryProperty);
            set => SetValue(ImageDirectoryProperty, value);
        }

        /// <summary>每个图片格子的边长(像素),默认 180。</summary>
        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(180.0));

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

        /// <summary>图片之间的间隔(像素),默认 6。</summary>
        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(6.0));

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        /// <summary>是否在悬浮时显示右上角删除按钮,默认 false。</summary>
        public static readonly DependencyProperty ShowDeleteButtonProperty =
            DependencyProperty.Register(nameof(ShowDeleteButton), typeof(bool),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(false));

        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }

        /// <summary>是否接受拖入文件/图片自动导入到 ImageDirectory,默认 false。</summary>
        public static readonly DependencyProperty AllowDropImportProperty =
            DependencyProperty.Register(nameof(AllowDropImport), typeof(bool),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(false, OnAllowDropImportChanged));

        public bool AllowDropImport
        {
            get => (bool)GetValue(AllowDropImportProperty);
            set => SetValue(AllowDropImportProperty, value);
        }

        private static void OnAllowDropImportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((JTImageGrid)d).AllowDrop = (bool)e.NewValue;



        /// <summary>是否允许把图片从列表拖出到外部(资源管理器/微信/浏览器等),默认 false。</summary>
        public static readonly DependencyProperty AllowDragOutProperty =
            DependencyProperty.Register(nameof(AllowDragOut), typeof(bool),
                typeof(JTImageGrid), new FrameworkPropertyMetadata(false));

        public bool AllowDragOut
        {
            get => (bool)GetValue(AllowDragOutProperty);
            set => SetValue(AllowDragOutProperty, value);
        }




        // ---------- 目录加载 ----------

        private static void OnImageDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((JTImageGrid)d).Reload();

        /// <summary>重新扫描目录加载图片(外部下载完图片后可调用刷新)。</summary>
        public void Reload()
        {
            _items.Clear();
            string? dir = ImageDirectory;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

            foreach (var file in Directory.EnumerateFiles(dir).Where(IsImageFile).OrderBy(f => f))
                _items.Add(new JTImageItem(file));
        }

        private static bool IsImageFile(string path) =>
            ImageExts.Contains(Path.GetExtension(path).ToLowerInvariant());

        // ---------- 容器进入可视区→解码,移出→取消 ----------

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (item is JTImageItem vm && vm.Thumbnail == null)
                _ = LoadAsync(vm);
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            if (item is JTImageItem vm && _loading.TryGetValue(vm, out var cts))
                try { cts.Cancel(); } catch { }
        }

        private async Task LoadAsync(JTImageItem vm)
        {
            if (_loading.ContainsKey(vm)) return;
            var cts = new CancellationTokenSource();
            _loading[vm] = cts;
            int size = ThumbnailSize;
            try
            {
                await _gate.WaitAsync(cts.Token);
                try
                {
                    var bmp = await Task.Run(() => Decode(vm.Path, size), cts.Token);
                    if (!cts.Token.IsCancellationRequested && bmp != null)
                    {
                        if (Dispatcher.CheckAccess())
                            vm.Thumbnail = bmp;
                        else
                            await Dispatcher.InvokeAsync(() => vm.Thumbnail = bmp);
                    }
                }
                finally { _gate.Release(); }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                if (_loading.TryGetValue(vm, out var c) && c == cts)
                    _loading.Remove(vm);
                cts.Dispose();
            }
        }

        private static BitmapSource? Decode(string path, int decodePixelWidth)
        {
            try
            {
                using var codec = SKCodec.Create(path);
                if (codec == null) return null;

                var srcInfo = codec.Info;
                if (srcInfo.Width <= 0 || srcInfo.Height <= 0) return null;

                int longest = Math.Max(srcInfo.Width, srcInfo.Height);
                float desired = decodePixelWidth >= longest ? 1f : (float)decodePixelWidth / longest;

                SKSizeI scaled = codec.GetScaledDimensions(desired);
                if (scaled.Width <= 0 || scaled.Height <= 0)
                    scaled = new SKSizeI(srcInfo.Width, srcInfo.Height);

                var info = new SKImageInfo(scaled.Width, scaled.Height,
                    SKColorType.Bgra8888, SKAlphaType.Premul);

                using var skBitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, skBitmap.GetPixels());
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                    return null;

                var wb = new WriteableBitmap(info.Width, info.Height, 96, 96,
                    PixelFormats.Pbgra32, null);
                wb.Lock();
                try
                {
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (void*)skBitmap.GetPixels(),
                            (void*)wb.BackBuffer,
                            (long)wb.BackBufferStride * wb.PixelHeight,
                            (long)info.RowBytes * info.Height);
                    }
                    wb.AddDirtyRect(new Int32Rect(0, 0, info.Width, info.Height));
                }
                finally { wb.Unlock(); }

                wb.Freeze();
                return wb;
            }
            catch
            {
                return null;
            }
        }

        // ---------- 点击 ----------

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
      

            _dragCandidate = null;

            if (_outDragging) { _outDragging = false; return; }   // 刚拖出过,不当点击

            if (IsInDeleteButton(e.OriginalSource)) return;
            if (GetItemFromEvent(e.OriginalSource) is JTImageItem vm)
                ImageLeftClick?.Invoke(vm.Path);
        }

        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);
            if (GetItemFromEvent(e.OriginalSource) is JTImageItem vm)
                ImageRightClick?.Invoke(vm.Path);
        }

        private static JTImageItem? GetItemFromEvent(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem item && item.DataContext is JTImageItem vm)
                    return vm;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        /// <summary>判断事件源是否落在删除按钮(Tag 为 JTImageItem 的 Button)内。</summary>
        private static bool IsInDeleteButton(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is Button btn && btn.Tag is JTImageItem) return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        // ---------- 删除 ----------

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is JTImageItem vm)
            {
                RemoveItemPreservingScroll(vm);
                e.Handled = true;
            }
        }

        private void RemoveItemPreservingScroll(JTImageItem vm)
        {
            double offset = _scrollViewer?.VerticalOffset ?? 0;

            _items.Remove(vm);
            ImageDeleted?.Invoke(vm.Path);

            if (_scrollViewer == null) return;

            void Restore(object? s, EventArgs e)
            {
                _scrollViewer.LayoutUpdated -= Restore;
                double max = _scrollViewer.ScrollableHeight;
                _scrollViewer.ScrollToVerticalOffset(Math.Min(offset, max));
            }
            _scrollViewer.LayoutUpdated += Restore;
        }

        // ---------- 拖入 ----------

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            e.Effects = (AllowDropImport && JTImageImporter.CanAccept(e.Data))
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        protected override async void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (!AllowDropImport || string.IsNullOrWhiteSpace(ImageDirectory)) return;
            e.Handled = true;

            var importer = new JTImageImporter(ImageDirectory!);
            var results = await importer.ImportFromDropAsync(e.Data);

            foreach (var r in results)
            {
                if (r.Success && r.FilePath != null)
                {
                    if (!_items.Any(i => string.Equals(i.Path, r.FilePath, StringComparison.OrdinalIgnoreCase)))
                        _items.Add(new JTImageItem(r.FilePath));
                    ImageImported?.Invoke(r.FilePath);
                }
                else
                {
                    ImportFailed?.Invoke(r.Reason, r.Source);
                }
            }
        }

        // ---------- 剪贴板(按钮调用) ----------

        /// <summary>把剪贴板里的图片粘贴进 ImageDirectory 并加入列表。返回是否成功。</summary>
        public bool PasteFromClipboard()
        {
            if (string.IsNullOrWhiteSpace(ImageDirectory)) return false;
            var importer = new JTImageImporter(ImageDirectory!);
            var r = importer.ImportFromClipboard();
            if (r.Success && r.FilePath != null)
            {
                _items.Add(new JTImageItem(r.FilePath));
                ImageImported?.Invoke(r.FilePath);
                return true;
            }
            ImportFailed?.Invoke(r.Reason, r.Source);
            return false;
        }




        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            _dragCandidate = null;
            _outDragging = false;

            if (!AllowDragOut) return;
            if (IsInDeleteButton(e.OriginalSource)) return;   // 点删除按钮不发起拖出

            if (GetItemFromEvent(e.OriginalSource) is JTImageItem vm &&
                File.Exists(vm.Path))
            {
                _dragCandidate = vm;
                _dragStart = e.GetPosition(this);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!AllowDragOut || _dragCandidate == null || e.LeftButton != MouseButtonState.Pressed)
                return;
            if (_outDragging) return;

            Point pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStart.X) < OutDragThreshold &&
                Math.Abs(pos.Y - _dragStart.Y) < OutDragThreshold)
                return;

            // 超过阈值,发起往外拖
            _outDragging = true;
            StartFileDragOut(_dragCandidate);
            _dragCandidate = null;
        }

        private void StartFileDragOut(JTImageItem origin)
        {
            // 收集要拖出的文件:优先用列表当前选中项,否则就拖起手的这一张
            var paths = SelectedItems.OfType<JTImageItem>()
                .Select(i => i.Path)
                .Where(File.Exists)
                .ToList();

            if (paths.Count == 0 && File.Exists(origin.Path))
                paths.Add(origin.Path);
            if (paths.Count == 0) return;

            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, paths.ToArray());
            // 同时附带文本路径,部分程序(如某些聊天框)只认文本
            data.SetData(DataFormats.Text, string.Join(Environment.NewLine, paths));

            try
            {
                DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
            }
            catch { /* 拖拽被中断,忽略 */ }
            finally
            {
                _outDragging = false;
            }
        }


    }
}

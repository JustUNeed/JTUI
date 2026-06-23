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
using GongSolutions.Wpf.DragDrop;

namespace JTUI.Controls.FileGrid
{
    /// <summary>
    /// 极简文件网格(纯 UI 列表控件)。
    /// 显示"系统图标 + 名称",维护一个路径列表,把用户操作抛给外部。
    /// 左键点击只抛路径(ItemClicked),不负责打开文件。
    /// 拖拽排序 / 拖入 / 删除 / 清空 都会触发 ListChanged,供外部持久化。
    /// 初始化由外部 SetItems 灌入,不触发 ListChanged。
    /// 拖拽由 gong-wpf-dragdrop 统一接管(排序 + 外部拖入 + 拖出)。
    /// </summary>
    public class JTFileGrid : ListBox
    {
        private readonly ObservableCollection<JTFileItem> _items = new();
        private readonly Dictionary<JTFileItem, CancellationTokenSource> _loading = new();
        private readonly SemaphoreSlim _gate = new(Environment.ProcessorCount);
        private readonly FileGridDropHandler _dropHandler;

        private ScrollViewer? _scrollViewer;

        static JTFileGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTFileGrid), new FrameworkPropertyMetadata(typeof(JTFileGrid)));
        }

        public JTFileGrid()
        {
            ItemsSource = _items;
            AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnDeleteButtonClick));

            _dropHandler = new FileGridDropHandler(this);

          
           

            // 用 gong 统一接管拖拽:本控件既是拖拽源(排序/拖出),又是放置目标(排序/拖入)。
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this, _dropHandler);

        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        }

        // ---------- 内部访问器(供 DropHandler 用) ----------

        internal ObservableCollection<JTFileItem> Items_ => _items;
        internal bool DistinctInternal => Distinct;
        internal bool AllowDropImportInternal => AllowDropImport;

        internal bool ContainsPath(string path) =>
            _items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));

        internal void NotifyListChanged() => RaiseListChanged();

        // ---------- 对外回调 ----------

        /// <summary>左键单击某一项,参数为该项路径。外部据此决定如何打开/启动。</summary>
        public event Action<string>? ItemClicked;

        /// <summary>右键单击某一项,参数为路径(供外部弹菜单等)。</summary>
        public event Action<string>? ItemRightClick;

        /// <summary>
        /// 列表内容发生变化(拖入 / 拖拽排序 / 删除 / 清空)时触发,参数为变更后的完整路径快照。
        /// 外部据此持久化列表。注意:SetItems(初始化/恢复)不触发本事件。
        /// </summary>
        public event Action<IReadOnlyList<string>>? ListChanged;

        private void RaiseListChanged() => ListChanged?.Invoke(GetPaths());

        // ---------- 依赖属性 ----------

        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(96.0));
        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register(nameof(IconSize), typeof(double),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(40.0));
        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(6.0));
        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public static readonly DependencyProperty ShowDeleteButtonProperty =
            DependencyProperty.Register(nameof(ShowDeleteButton), typeof(bool),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(false));
        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }

        /// <summary>是否接受从外部拖入文件追加到列表,默认 false。</summary>
        public static readonly DependencyProperty AllowDropImportProperty =
            DependencyProperty.Register(nameof(AllowDropImport), typeof(bool),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(false));
        public bool AllowDropImport
        {
            get => (bool)GetValue(AllowDropImportProperty);
            set => SetValue(AllowDropImportProperty, value);
        }

        /// <summary>是否允许列表内拖拽排序,默认 true。</summary>
        public static readonly DependencyProperty AllowReorderProperty =
            DependencyProperty.Register(nameof(AllowReorder), typeof(bool),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(true));
        public bool AllowReorder
        {
            get => (bool)GetValue(AllowReorderProperty);
            set => SetValue(AllowReorderProperty, value);
        }

        public static readonly DependencyProperty DistinctProperty =
            DependencyProperty.Register(nameof(Distinct), typeof(bool),
                typeof(JTFileGrid), new FrameworkPropertyMetadata(true));
        public bool Distinct
        {
            get => (bool)GetValue(DistinctProperty);
            set => SetValue(DistinctProperty, value);
        }

        internal bool AllowReorderInternal => AllowReorder;

        // ---------- 数据填充(外部接口) ----------

        /// <summary>【初始化/恢复】用一批路径替换整个列表。不触发 ListChanged。</summary>
        public void SetItems(IEnumerable<string> paths)
        {
            CancelAllLoading();
            _items.Clear();
            if (paths == null) return;
            foreach (var p in paths)
                AddPathSilent(p);
        }

        /// <summary>【追加单个】成功返回 true 并触发 ListChanged。</summary>
        public bool AddItem(string path)
        {
            if (!AddPathSilent(path)) return false;
            RaiseListChanged();
            return true;
        }

        /// <summary>【追加批量】只在末尾触发一次 ListChanged。</summary>
        public void AddItems(IEnumerable<string> paths)
        {
            if (paths == null) return;
            bool any = false;
            foreach (var p in paths)
                if (AddPathSilent(p)) any = true;
            if (any) RaiseListChanged();
        }

        public bool RemoveItem(string path)
        {
            var vm = _items.FirstOrDefault(i =>
                string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
            if (vm == null) return false;
            RemoveItemPreservingScroll(vm);
            return true;
        }

        public void Clear()
        {
            CancelAllLoading();
            if (_items.Count == 0) return;
            _items.Clear();
            RaiseListChanged();
        }

        public IReadOnlyList<string> GetPaths() => _items.Select(i => i.Path).ToList();

        internal bool AddPathSilent(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (Distinct && ContainsPath(path)) return false;
            _items.Add(new JTFileItem(path));
            return true;
        }

        // ---------- 图标按需提取 ----------

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (item is JTFileItem vm && vm.Icon == null)
                _ = LoadIconAsync(vm);
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            if (item is JTFileItem vm && _loading.TryGetValue(vm, out var cts))
                try { cts.Cancel(); } catch { }
        }

        private async Task LoadIconAsync(JTFileItem vm)
        {
            if (_loading.ContainsKey(vm)) return;
            var cts = new CancellationTokenSource();
            _loading[vm] = cts;
            try
            {
                await _gate.WaitAsync(cts.Token);
                try
                {
                    var icon = await Task.Run(
                        () => JTFileIconExtractor.Extract(vm.Path, large: true), cts.Token);
                    if (!cts.Token.IsCancellationRequested && icon != null)
                    {
                        if (Dispatcher.CheckAccess())
                            vm.Icon = icon;
                        else
                            await Dispatcher.InvokeAsync(() => vm.Icon = icon);
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

        private void CancelAllLoading()
        {
            foreach (var cts in _loading.Values)
                try { cts.Cancel(); } catch { }
            _loading.Clear();
        }

        // ---------- 点击:只抛路径 ----------

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            if (IsInDeleteButton(e.OriginalSource)) return;

            // gong 在真正拖拽时会接管事件,这里收到的 up 即视为点击。
            if (GetItemFromEvent(e.OriginalSource) is JTFileItem vm)
                ItemClicked?.Invoke(vm.Path);
        }

        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);
            if (GetItemFromEvent(e.OriginalSource) is JTFileItem vm)
                ItemRightClick?.Invoke(vm.Path);
        }

        internal static JTFileItem? GetItemFromEvent(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem item && item.DataContext is JTFileItem vm)
                    return vm;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        private static bool IsInDeleteButton(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is Button btn && btn.Tag is JTFileItem) return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        // ---------- 删除 ----------

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is JTFileItem vm)
            {
                RemoveItemPreservingScroll(vm);
                e.Handled = true;
            }
        }

        private void RemoveItemPreservingScroll(JTFileItem vm)
        {
            double offset = _scrollViewer?.VerticalOffset ?? 0;

            if (_loading.TryGetValue(vm, out var cts))
                try { cts.Cancel(); } catch { }

            _items.Remove(vm);
            RaiseListChanged();

            if (_scrollViewer == null) return;

            void Restore(object? s, EventArgs e)
            {
                _scrollViewer.LayoutUpdated -= Restore;
                double max = _scrollViewer.ScrollableHeight;
                _scrollViewer.ScrollToVerticalOffset(Math.Min(offset, max));
            }
            _scrollViewer.LayoutUpdated += Restore;
        }
    }
}

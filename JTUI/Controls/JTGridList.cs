using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GongSolutions.Wpf.DragDrop;
using GongDragDrop = GongSolutions.Wpf.DragDrop.DragDrop;

namespace JTUI.Controls
{
    /// <summary>
    /// 通用网格列表控件。
    /// 职责:呈现(网格布局) + 交互(点击/右键/拖拽排序/拖入/拖出)。
    /// 数据变动(排序、拖入插入、删除)直接作用在用户提供的可写 ItemsSource 上;
    /// 业务逻辑由用户在 C# 侧编写。
    ///
    /// 用法要点:
    ///  - ItemsSource 绑定可写集合(推荐 ObservableCollection&lt;T&gt;)。
    ///  - ItemTemplate 自定义每项外观;模板里可放任意按钮,Click 正常冒泡给用户。
    ///  - 删除:用户在自己的按钮 Click 里调用 DeleteItem(item)。
    ///  - 拖入:AllowDropImport=true 且设置 DropHandler,把 IDataObject 翻译成数据项。
    ///  - 拖出:AllowDragOut=true 且设置 DragOutHandler,返回 DataObject。
    ///  - 异步内容:设置 LoadItemContentAsync,控件负责限流/可视区取消。
    /// </summary>
    public class JTGridList : ListBox, IDragSource, IDropTarget
    {
        private readonly InternalDropTarget _dropTarget;
        private ScrollViewer? _scrollViewer;

        // 异步加载调度
        private readonly Dictionary<object, CancellationTokenSource> _loading = new();
        private readonly SemaphoreSlim _gate = new(Environment.ProcessorCount);

        static JTGridList()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTGridList), new FrameworkPropertyMetadata(typeof(JTGridList)));
        }

        public JTGridList()
        {
            _dropTarget = new InternalDropTarget(this);

            GongDragDrop.SetIsDragSource(this, true);
            GongDragDrop.SetIsDropTarget(this, true);
            GongDragDrop.SetDropHandler(this, _dropTarget);
            GongDragDrop.SetDragHandler(this, this);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        }

        // ============================================================
        // 依赖属性:布局
        // ============================================================

        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double),
                typeof(JTGridList), new FrameworkPropertyMetadata(120.0, OnLayoutChanged));
        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(JTGridList), new FrameworkPropertyMetadata(120.0, OnLayoutChanged));
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double),
                typeof(JTGridList), new FrameworkPropertyMetadata(6.0, OnLayoutChanged));
        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        /// <summary>最多显示行数,超出则内部滚动;0 表示不限高。默认 0。</summary>
        public static readonly DependencyProperty MaxRowsProperty =
            DependencyProperty.Register(nameof(MaxRows), typeof(int),
                typeof(JTGridList), new FrameworkPropertyMetadata(0, OnLayoutChanged));
        public int MaxRows
        {
            get => (int)GetValue(MaxRowsProperty);
            set => SetValue(MaxRowsProperty, value);
        }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((JTGridList)d).UpdateAutoHeight();

        // ============================================================
        // 依赖属性:行为开关
        // ============================================================

        public static readonly DependencyProperty AllowReorderProperty =
            DependencyProperty.Register(nameof(AllowReorder), typeof(bool),
                typeof(JTGridList), new FrameworkPropertyMetadata(true));
        public bool AllowReorder
        {
            get => (bool)GetValue(AllowReorderProperty);
            set => SetValue(AllowReorderProperty, value);
        }

        /// <summary>是否接受外部拖入(需同时设置 DropHandler)。默认 false。</summary>
        public static readonly DependencyProperty AllowDropImportProperty =
            DependencyProperty.Register(nameof(AllowDropImport), typeof(bool),
                typeof(JTGridList), new FrameworkPropertyMetadata(false));
        public bool AllowDropImport
        {
            get => (bool)GetValue(AllowDropImportProperty);
            set => SetValue(AllowDropImportProperty, value);
        }

        /// <summary>是否允许把项拖出到外部程序(需同时设置 DragOutHandler)。默认 false。</summary>
        public static readonly DependencyProperty AllowDragOutProperty =
            DependencyProperty.Register(nameof(AllowDragOut), typeof(bool),
                typeof(JTGridList), new FrameworkPropertyMetadata(false));
        public bool AllowDragOut
        {
            get => (bool)GetValue(AllowDragOutProperty);
            set => SetValue(AllowDragOutProperty, value);
        }

        // ============================================================
        // 用户扩展点(委托)
        // ============================================================

        /// <summary>
        /// 拖入处理:把拖入的数据翻译成要插入集合的对象。
        /// 参数:(拖入数据, 落点目标项或 null, 建议插入索引)。
        /// 返回要插入的对象序列(可空/可多个),控件按索引插入可写集合。
        /// 若你想完全自己处理(如移动磁盘文件),在委托内做完并返回 null/空即可。
        /// </summary>
        public Func<IDataObject, object?, int, IEnumerable<object>?>? DropHandler { get; set; }

        /// <summary>判断是否接受这次拖入(控制鼠标效果)。为 null 时只要 AllowDropImport 即接受。</summary>
        public Func<IDataObject, object?, bool>? CanAcceptDrop { get; set; }

        /// <summary>
        /// 拖出处理:把要拖出的项打包成 DataObject(如塞 FileDrop 路径)。
        /// 参数为当前要拖出的项集合(多选时为所有选中项,否则为起拖项)。返回 null 则不发起拖出。
        /// </summary>
        public Func<IReadOnlyList<object>, IDataObject?>? DragOutHandler { get; set; }

        /// <summary>异步内容加载:项进入可视区时调用,你负责加载并赋值(解码缩略图/提取图标等)。</summary>
        public Func<object, CancellationToken, Task>? LoadItemContentAsync { get; set; }

        // ============================================================
        // 对外事件
        // ============================================================

        public event EventHandler<JTGridItemEventArgs>? ItemClicked;
        public event EventHandler<JTGridItemEventArgs>? ItemRightClicked;
        public event EventHandler<JTGridItemCancelEventArgs>? ItemDeleting;
        public event EventHandler<JTGridItemEventArgs>? ItemDeleted;
        public event EventHandler<JTGridReorderCancelEventArgs>? ItemsReordering;
        public event EventHandler<JTGridReorderEventArgs>? ItemsReordered;
        /// <summary>拖入并插入完成。参数为新插入的对象列表。</summary>
        public event EventHandler<JTGridDropEventArgs>? ItemsDropped;

        // ============================================================
        // 集合可写性
        // ============================================================

        internal IList? MutableSource =>
            (ItemsSource as IList) is { IsReadOnly: false, IsFixedSize: false } list ? list : null;

        // ============================================================
        // 点击 / 右键
        // ============================================================

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            var item = GetItemFromEvent(e.OriginalSource);
            if (item != null)
                ItemClicked?.Invoke(this, new JTGridItemEventArgs(item));
        }

        protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);
            var item = GetItemFromEvent(e.OriginalSource);
            if (item != null)
                ItemRightClicked?.Invoke(this, new JTGridItemEventArgs(item));
        }

        // ============================================================
        // 删除(供用户在自己的按钮 Click 里调用)
        // ============================================================

        /// <summary>删除一项(三段式:ItemDeleting 可取消 → 移除 → ItemDeleted,自动保滚动)。</summary>
        public bool DeleteItem(object item)
        {
            var list = MutableSource;
            bool inList = list != null && list.Contains(item);

            var cancel = new JTGridItemCancelEventArgs(item);
            ItemDeleting?.Invoke(this, cancel);
            if (cancel.Cancel) return false;

            double offset = _scrollViewer?.VerticalOffset ?? 0;

            if (_loading.TryGetValue(item, out var cts))
                try { cts.Cancel(); } catch { }

            if (list != null && inList)
                list.Remove(item);

            ItemDeleted?.Invoke(this, new JTGridItemEventArgs(item));
            RestoreScroll(offset);
            return true;
        }

        // ============================================================
        // 异步内容加载调度
        // ============================================================

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (LoadItemContentAsync != null && item != null && !_loading.ContainsKey(item))
                _ = RunLoadAsync(item);
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);
            if (item != null && _loading.TryGetValue(item, out var cts))
            {
                try { cts.Cancel(); } catch { }
                _loading.Remove(item);
            }
        }

        private async Task RunLoadAsync(object item)
        {
            var load = LoadItemContentAsync;
            if (load == null) return;

            var cts = new CancellationTokenSource();
            _loading[item] = cts;
            try
            {
                await _gate.WaitAsync(cts.Token);
                try
                {
                    if (!cts.Token.IsCancellationRequested)
                        await load(item, cts.Token);
                }
                finally { _gate.Release(); }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                if (_loading.TryGetValue(item, out var c) && c == cts)
                    _loading.Remove(item);
                cts.Dispose();
            }
        }

        // ============================================================
        // IDragSource(仅在需要"拖出到外部"时介入;排序用 gong 默认逻辑)
        // ============================================================

        void IDragSource.StartDrag(IDragInfo dragInfo)
        {
            // 先走 gong 默认起拖(设置 Data/Effects,保证内部排序正常)
            GongDragDrop.DefaultDragHandler.StartDrag(dragInfo);

            // 仅当开启拖出且用户提供打包器时,替换为自定义 DataObject
            if (AllowDragOut && DragOutHandler != null)
            {
                var items = dragInfo.SourceItems?.Cast<object>().ToList() ?? new List<object>();
                if (items.Count == 0 && dragInfo.SourceItem != null)
                    items.Add(dragInfo.SourceItem);

                if (items.Count > 0)
                {
                    var data = DragOutHandler(items);
                    if (data != null)
                    {
                        dragInfo.DataObject = data;
                        dragInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                    }
                }
            }
        }

        bool IDragSource.CanStartDrag(IDragInfo dragInfo) => AllowReorder || AllowDragOut;

        void IDragSource.Dropped(IDropInfo dropInfo) { }
        void IDragSource.DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo) { }
        void IDragSource.DragCancelled() { }
        bool IDragSource.TryCatchOccurredException(Exception exception) => false;


        // ============================================================
        // IDropTarget(转发给内部实现)
        // ============================================================

        void IDropTarget.DragOver(IDropInfo dropInfo) => _dropTarget.DragOver(dropInfo);
        void IDropTarget.Drop(IDropInfo dropInfo) => _dropTarget.Drop(dropInfo);

        // 供内部 DropTarget 调用
        internal void RaiseReordering(JTGridReorderCancelEventArgs e) => ItemsReordering?.Invoke(this, e);
        internal void RaiseReordered(JTGridReorderEventArgs e) => ItemsReordered?.Invoke(this, e);
        internal void RaiseDropped(JTGridDropEventArgs e) => ItemsDropped?.Invoke(this, e);

        // ============================================================
        // 命中测试
        // ============================================================

        internal object? GetItemFromEvent(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem lbi) return lbi.DataContext;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        // ============================================================
        // 自适应高度
        // ============================================================

        private void UpdateAutoHeight()
        {
            if (MaxRows <= 0) { ClearValue(HeightProperty); return; }

            double usable = ActualWidth - Padding.Left - Padding.Right
                            - BorderThickness.Left - BorderThickness.Right;
            if (usable <= 0) return;

            int count = Items.Count;
            if (count == 0) { ClearValue(HeightProperty); return; }

            int cols = Math.Max(1, (int)((usable + ItemSpacing) / (ColumnWidth + ItemSpacing)));
            int rows = (int)Math.Ceiling(count / (double)cols);
            int show = Math.Min(rows, MaxRows);

            double height = show * ItemHeight + Math.Max(0, show - 1) * ItemSpacing
                            + Padding.Top + Padding.Bottom
                            + BorderThickness.Top + BorderThickness.Bottom;
            Height = height;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.WidthChanged) UpdateAutoHeight();
        }

        protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            UpdateAutoHeight();
        }

        private void RestoreScroll(double offset)
        {
            if (_scrollViewer == null) return;
            void Restore(object? s, EventArgs e)
            {
                _scrollViewer!.LayoutUpdated -= Restore;
                _scrollViewer.ScrollToVerticalOffset(Math.Min(offset, _scrollViewer.ScrollableHeight));
            }
            _scrollViewer.LayoutUpdated += Restore;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;

namespace JTUI.Controls.TextList
{
    /// <summary>
    /// 极简文字列表(纯 UI 列表控件)。
    /// 垂直排列,每段文本一项;文本按控件宽度省略号缩略。
    /// 左/右键点击只抛文本(ItemClicked / ItemRightClick),不负责具体动作。
    /// 拖入文本新增一项、拖拽排序、拖出文本到外部(文本框/浏览器)、删除/清空 都会触发 ListChanged。
    /// 初始化由外部 SetItems 灌入,不触发 ListChanged。
    /// </summary>
    public class JTTextList : ListBox
    {
        private readonly ObservableCollection<JTTextItem> _items = new();
        private readonly TextListDropHandler _dropHandler;

        static JTTextList()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTTextList), new FrameworkPropertyMetadata(typeof(JTTextList)));
        }

        public JTTextList()
        {
            ItemsSource = _items;
            AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnDeleteButtonClick));

            _dropHandler = new TextListDropHandler(this);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this, _dropHandler);

            MouseRightButtonUp += OnMouseRightButtonUp;
            PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;


            // 新增：自适应高度
            _items.CollectionChanged += (_, _) => UpdateAutoHeight();
        }

        // ---------- 供 DropHandler 用的内部访问 ----------
        internal ObservableCollection<JTTextItem> Items_ => _items;
        internal bool DistinctInternal => Distinct;
        internal bool AllowDropImportInternal => AllowDropImport;
        internal bool AllowReorderInternal => AllowReorder;
        internal bool ContainsText(string text) =>
            _items.Any(i => string.Equals(i.Text, text,
                Distinct ? StringComparison.Ordinal : StringComparison.Ordinal));
        internal void NotifyListChanged() => RaiseListChanged();

        // ---------- 对外回调 ----------

        /// <summary>左键单击某一项,参数为该项文本。</summary>
        public event Action<string>? ItemClicked;

        /// <summary>右键单击某一项,参数为该项文本(供外部弹菜单等)。</summary>
        public event Action<string>? ItemRightClick;

        /// <summary>列表内容变化(拖入/排序/删除/清空)时触发,参数为变更后的完整文本快照。SetItems 不触发。</summary>
        public event Action<IReadOnlyList<string>>? ListChanged;

        private void RaiseListChanged() => ListChanged?.Invoke(GetTexts());

        // ---------- 依赖属性 ----------

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(JTTextList), new FrameworkPropertyMetadata(28.0));
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public static readonly DependencyProperty ShowDeleteButtonProperty =
            DependencyProperty.Register(nameof(ShowDeleteButton), typeof(bool),
                typeof(JTTextList), new FrameworkPropertyMetadata(false));
        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }

        /// <summary>空列表提示文字。</summary>
        public static readonly DependencyProperty EmptyHintProperty =
            DependencyProperty.Register(nameof(EmptyHint), typeof(string),
                typeof(JTTextList), new FrameworkPropertyMetadata("拖入文本"));
        public string EmptyHint
        {
            get => (string)GetValue(EmptyHintProperty);
            set => SetValue(EmptyHintProperty, value);
        }

        /// <summary>是否接受从外部拖入文本追加,默认 false。</summary>
        public static readonly DependencyProperty AllowDropImportProperty =
            DependencyProperty.Register(nameof(AllowDropImport), typeof(bool),
                typeof(JTTextList), new FrameworkPropertyMetadata(false));
        public bool AllowDropImport
        {
            get => (bool)GetValue(AllowDropImportProperty);
            set => SetValue(AllowDropImportProperty, value);
        }

        /// <summary>是否允许列表内拖拽排序,默认 true。</summary>
        public static readonly DependencyProperty AllowReorderProperty =
            DependencyProperty.Register(nameof(AllowReorder), typeof(bool),
                typeof(JTTextList), new FrameworkPropertyMetadata(true));
        public bool AllowReorder
        {
            get => (bool)GetValue(AllowReorderProperty);
            set => SetValue(AllowReorderProperty, value);
        }

        /// <summary>是否去重(完全相同的文本不重复加入),默认 true。</summary>
        public static readonly DependencyProperty DistinctProperty =
            DependencyProperty.Register(nameof(Distinct), typeof(bool),
                typeof(JTTextList), new FrameworkPropertyMetadata(true));
        public bool Distinct
        {
            get => (bool)GetValue(DistinctProperty);
            set => SetValue(DistinctProperty, value);
        }


        /// <summary>最多显示的行数，超过则内部滚动。设为 0 表示不限制。默认 3。</summary>
        public static readonly DependencyProperty MaxRowsProperty =
            DependencyProperty.Register(nameof(MaxRows), typeof(int), typeof(JTTextList),
                new FrameworkPropertyMetadata(3, (d, _) => ((JTTextList)d).UpdateAutoHeight()));
        public int MaxRows
        {
            get => (int)GetValue(MaxRowsProperty);
            set => SetValue(MaxRowsProperty, value);
        }



        private void UpdateAutoHeight()
        {
            if (MaxRows <= 0) { ClearValue(HeightProperty); return; }

            int count = _items.Count;
            if (count == 0) { ClearValue(HeightProperty); return; }

            int show = Math.Min(count, MaxRows);

            Height = show * ItemHeight
                     + Padding.Top + Padding.Bottom
                     + BorderThickness.Top + BorderThickness.Bottom;
        }



        // ---------- 数据填充(外部接口) ----------

        /// <summary>【初始化/恢复】用一批文本替换整个列表。不触发 ListChanged。</summary>
        public void SetItems(IEnumerable<string> texts)
        {
            _items.Clear();
            if (texts == null) return;
            foreach (var t in texts)
                AddTextSilent(t);
        }

        /// <summary>【追加单个】成功返回 true 并触发 ListChanged。</summary>
        public bool AddItem(string text)
        {
            if (!AddTextSilent(text)) return false;
            RaiseListChanged();
            return true;
        }

        /// <summary>【追加批量】只在末尾触发一次 ListChanged。</summary>
        public void AddItems(IEnumerable<string> texts)
        {
            if (texts == null) return;
            bool any = false;
            foreach (var t in texts)
                if (AddTextSilent(t)) any = true;
            if (any) RaiseListChanged();
        }

        public bool RemoveItem(string text)
        {
            var vm = _items.FirstOrDefault(i => string.Equals(i.Text, text, StringComparison.Ordinal));
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

        public IReadOnlyList<string> GetTexts() => _items.Select(i => i.Text).ToList();

        internal bool AddTextSilent(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Trim();
            if (text.Length == 0) return false;
            if (Distinct && ContainsText(text)) return false;
            _items.Add(new JTTextItem(text));
            return true;
        }

        // ---------- 删除角标 ----------

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.Tag is JTTextItem vm)
            {
                _items.Remove(vm);
                RaiseListChanged();
                e.Handled = true;   // 阻止冒泡成项点击
            }
        }

        // ---------- 左/右键点击 ----------

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ItemFromEvent(e) is JTTextItem vm)
                ItemClicked?.Invoke(vm.Text);
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ItemFromEvent(e) is JTTextItem vm)
                ItemRightClick?.Invoke(vm.Text);
        }

        private JTTextItem? ItemFromEvent(MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not ListBoxItem)
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            return (dep as ListBoxItem)?.DataContext as JTTextItem;
        }
    }
}

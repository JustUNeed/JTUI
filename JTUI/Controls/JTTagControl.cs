using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTUI.Controls
{
    /// <summary>
    /// 标签集合控件。绑定字符串集合,自动渲染为可点击 / 删除 / 重命名 / 新增的标签。
    /// 自动去重(默认不区分大小写),并对文本做规范校验。
    /// 提供公开方法(AddTag/RemoveTag/RenameTag)与可取消事件供外部接入。
    /// </summary>
    [TemplatePart(Name = PartInput, Type = typeof(JTTextBox))]
    public class JTTagControl : ItemsControl
    {
        private const string PartAddButton = "PART_AddButton";
        private const string PartInputHost = "PART_InputHost";
        private const string PartInput = "PART_Input";

        private FrameworkElement? _addButton;
        private FrameworkElement? _inputHost;
        private JTTextBox? _input;



        static JTTagControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTTagControl),
                new FrameworkPropertyMetadata(typeof(JTTagControl)));
        }

        public JTTagControl()
        {
            AddHandler(JTTag.ClickEvent, new RoutedEventHandler(OnTagClick));
            AddHandler(JTTag.DeleteRequestedEvent, new RoutedEventHandler(OnTagDeleteRequested));
            AddHandler(JTTag.RenameCommittedEvent, new TagRenameEventHandler(OnTagRenameCommitted));
        }

        // ---------- 可配置项 ----------

        /// <summary>去重是否区分大小写,默认 false(不区分)。</summary>
        public static readonly DependencyProperty CaseSensitiveProperty =
            DependencyProperty.Register(
                nameof(CaseSensitive), typeof(bool), typeof(JTTagControl),
                new FrameworkPropertyMetadata(false));
        public bool CaseSensitive
        {
            get => (bool)GetValue(CaseSensitiveProperty);
            set => SetValue(CaseSensitiveProperty, value);
        }

        /// <summary>是否显示末尾新增输入框,默认 true。</summary>
        public static readonly DependencyProperty AllowAddProperty =
            DependencyProperty.Register(
                nameof(AllowAdd), typeof(bool), typeof(JTTagControl),
                new FrameworkPropertyMetadata(true));
        public bool AllowAdd
        {
            get => (bool)GetValue(AllowAddProperty);
            set => SetValue(AllowAddProperty, value);
        }

        /// <summary>新增输入框占位提示。</summary>
        public static readonly DependencyProperty AddPlaceholderProperty =
            DependencyProperty.Register(
                nameof(AddPlaceholder), typeof(string), typeof(JTTagControl),
                new FrameworkPropertyMetadata("添加标签…"));
        public string AddPlaceholder
        {
            get => (string)GetValue(AddPlaceholderProperty);
            set => SetValue(AddPlaceholderProperty, value);
        }

        /// <summary>
        /// 自定义校验/规范化委托。输入原始文本,返回 (是否通过, 规范化后的文本)。
        /// 为 null 时使用内置默认规则(去首尾空格 + 非空)。
        /// </summary>
        public Func<string, (bool ok, string normalized)>? TagValidator { get; set; }

        // ---------- 路由事件(供外部监听 / 拦截) ----------

        // before(可取消)
        public static readonly RoutedEvent TagAddingEvent =
            EventManager.RegisterRoutedEvent(nameof(TagAdding), RoutingStrategy.Bubble,
                typeof(TagChangingEventHandler), typeof(JTTagControl));
        public event TagChangingEventHandler TagAdding
        { add => AddHandler(TagAddingEvent, value); remove => RemoveHandler(TagAddingEvent, value); }

        public static readonly RoutedEvent TagRemovingEvent =
            EventManager.RegisterRoutedEvent(nameof(TagRemoving), RoutingStrategy.Bubble,
                typeof(TagChangingEventHandler), typeof(JTTagControl));
        public event TagChangingEventHandler TagRemoving
        { add => AddHandler(TagRemovingEvent, value); remove => RemoveHandler(TagRemovingEvent, value); }

        public static readonly RoutedEvent TagRenamingEvent =
            EventManager.RegisterRoutedEvent(nameof(TagRenaming), RoutingStrategy.Bubble,
                typeof(TagRenameEventHandler), typeof(JTTagControl));
        public event TagRenameEventHandler TagRenaming
        { add => AddHandler(TagRenamingEvent, value); remove => RemoveHandler(TagRenamingEvent, value); }

        // after
        public static readonly RoutedEvent TagAddedEvent =
            EventManager.RegisterRoutedEvent(nameof(TagAdded), RoutingStrategy.Bubble,
                typeof(TagChangedEventHandler), typeof(JTTagControl));
        public event TagChangedEventHandler TagAdded
        { add => AddHandler(TagAddedEvent, value); remove => RemoveHandler(TagAddedEvent, value); }

        public static readonly RoutedEvent TagRemovedEvent =
            EventManager.RegisterRoutedEvent(nameof(TagRemoved), RoutingStrategy.Bubble,
                typeof(TagChangedEventHandler), typeof(JTTagControl));
        public event TagChangedEventHandler TagRemoved
        { add => AddHandler(TagRemovedEvent, value); remove => RemoveHandler(TagRemovedEvent, value); }

        public static readonly RoutedEvent TagRenamedEvent =
            EventManager.RegisterRoutedEvent(nameof(TagRenamed), RoutingStrategy.Bubble,
                typeof(TagRenameEventHandler), typeof(JTTagControl));
        public event TagRenameEventHandler TagRenamed
        { add => AddHandler(TagRenamedEvent, value); remove => RemoveHandler(TagRenamedEvent, value); }

        public static readonly RoutedEvent TagClickedEvent =
            EventManager.RegisterRoutedEvent(nameof(TagClicked), RoutingStrategy.Bubble,
                typeof(TagChangedEventHandler), typeof(JTTagControl));
        public event TagChangedEventHandler TagClicked
        { add => AddHandler(TagClickedEvent, value); remove => RemoveHandler(TagClickedEvent, value); }

        /// <summary>校验失败时触发,携带原因。</summary>
        public static readonly RoutedEvent TagRejectedEvent =
            EventManager.RegisterRoutedEvent(nameof(TagRejected), RoutingStrategy.Bubble,
                typeof(TagRejectedEventHandler), typeof(JTTagControl));
        public event TagRejectedEventHandler TagRejected
        { add => AddHandler(TagRejectedEvent, value); remove => RemoveHandler(TagRejectedEvent, value); }

        // ---------- 容器:用 JTTag 承载每个字符串 ----------

        protected override DependencyObject GetContainerForItemOverride() => new JTTag();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is JTTag;

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is JTTag tag)
                tag.Text = item?.ToString() ?? string.Empty;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_addButton != null) _addButton.MouseLeftButtonUp -= OnAddClick;
            if (_input != null)
            {
                _input.KeyDown -= OnInputKeyDown;
                _input.LostKeyboardFocus -= OnInputLostFocus;
            }

            _addButton = GetTemplateChild(PartAddButton) as FrameworkElement;
            _inputHost = GetTemplateChild(PartInputHost) as FrameworkElement;
            _input = GetTemplateChild(PartInput) as JTTextBox;

            if (_addButton != null) _addButton.MouseLeftButtonUp += OnAddClick;
            if (_input != null)
            {
                _input.KeyDown += OnInputKeyDown;
                _input.LostKeyboardFocus += OnInputLostFocus;
            }
        }

        private void OnAddClick(object sender, RoutedEventArgs e) => BeginAddTag();

        /// <summary>显示临时输入框开始新建(也可外部调用)。</summary>
        public void BeginAddTag()
        {
            if (_inputHost == null || _input == null) return;
            if (_addButton != null) _addButton.Visibility = Visibility.Collapsed;
            _inputHost.Visibility = Visibility.Visible;
            _input.Text = string.Empty;
            _input.Focus();
        }

        private void EndAddTag()
        {
            if (_inputHost != null) _inputHost.Visibility = Visibility.Collapsed;
            if (_addButton != null) _addButton.Visibility = Visibility.Visible;
            if (_input != null) _input.Text = string.Empty;
        }

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _input != null)
            {
                if (AddTag(_input.Text))
                    _input.Text = string.Empty;   // 成功:清空,留在输入态继续连续添加
                                                  // 失败时保留文本让用户改;TagRejected 事件照常触发
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                EndAddTag();                       // 取消,收回成加号
                e.Handled = true;
            }
        }

        private void OnInputLostFocus(object? sender, RoutedEventArgs e)
        {
            // 失焦:有内容就尝试提交,然后收回
            if (_input != null && !string.IsNullOrWhiteSpace(_input.Text))
                AddTag(_input.Text);
            EndAddTag();
        }

    

        // ---------- 内部子项事件处理 ----------

        private void OnTagClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is JTTag tag)
                RaiseEvent(new TagChangedEventArgs(TagClickedEvent, this, tag.Text));
        }

        private void OnTagDeleteRequested(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is JTTag tag)
                RemoveTag(tag.Text);
        }

        private void OnTagRenameCommitted(object sender, TagRenameEventArgs e)
        {
            if (e.OriginalSource is JTTag tag)
                RenameTag(e.OldText, e.NewText);
        }

        // ---------- 公开接口 ----------

        /// <summary>新增标签。返回是否成功。校验失败 / 重复时返回 false 并触发 TagRejected。</summary>
        public bool AddTag(string text)
        {
            if (!Normalize(text, out string norm, out string reason))
            { Reject(text, reason); return false; }

            if (Contains(norm))
            { Reject(text, "标签已存在"); return false; }

            var adding = new TagChangingEventArgs(TagAddingEvent, this, norm);
            RaiseEvent(adding);
            if (adding.Cancel) return false;

            var list = EnsureMutableList();
            if (list == null) { Reject(text, "数据源不可写"); return false; }
            list.Add(norm);

            RaiseEvent(new TagChangedEventArgs(TagAddedEvent, this, norm));
            return true;
        }

        /// <summary>删除标签。返回是否成功。</summary>
        public bool RemoveTag(string text)
        {
            var list = EnsureMutableList();
            if (list == null) return false;

            object? target = FindItem(text);
            if (target == null) return false;

            var removing = new TagChangingEventArgs(TagRemovingEvent, this, text);
            RaiseEvent(removing);
            if (removing.Cancel) return false;

            list.Remove(target);
            RaiseEvent(new TagChangedEventArgs(TagRemovedEvent, this, text));
            return true;
        }

        /// <summary>重命名标签。返回是否成功。</summary>
        public bool RenameTag(string oldText, string newText)
        {
            if (!Normalize(newText, out string norm, out string reason))
            { Reject(newText, reason); return false; }

            // 名字没变(规范化后相同)直接当成功,不动集合
            if (Equals(norm, oldText)) return true;

            if (Contains(norm))
            { Reject(newText, "标签已存在"); return false; }

            var list = EnsureMutableList();
            if (list == null) return false;
            int idx = IndexOf(oldText);
            if (idx < 0) return false;

            var renaming = new TagRenameEventArgs(TagRenamingEvent, this, oldText, norm);
            RaiseEvent(renaming);
            if (renaming.Cancel) return false;

            list[idx] = norm;
            RaiseEvent(new TagRenameEventArgs(TagRenamedEvent, this, oldText, norm));
            return true;
        }

        // ---------- 校验 / 工具 ----------

        private bool Normalize(string raw, out string normalized, out string reason)
        {
            if (TagValidator != null)
            {
                var (ok, n) = TagValidator(raw ?? string.Empty);
                normalized = n ?? string.Empty;
                reason = ok ? string.Empty : "未通过自定义校验";
                return ok;
            }

            // 内置默认规则:去首尾空白、合并内部多余空白、非空
            normalized = (raw ?? string.Empty).Trim();
            if (normalized.Length == 0)
            { reason = "标签不能为空"; return false; }
            // 折叠中间连续空白为单个空格
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
            reason = string.Empty;
            return true;
        }

        private bool Equals(string a, string b) =>
            string.Equals(a, b,
                CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

        private IEnumerable<object> Items_ =>
            (ItemsSource as IEnumerable)?.Cast<object>() ?? Items.Cast<object>();

        private bool Contains(string text) =>
            Items_.Any(i => Equals(i?.ToString() ?? "", text));

        private object? FindItem(string text) =>
            Items_.FirstOrDefault(i => Equals(i?.ToString() ?? "", text));

        private int IndexOf(string text)
        {
            var list = EnsureMutableList();
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (Equals(list[i]?.ToString() ?? "", text)) return i;
            return -1;
        }

        // 取可写集合:优先 ItemsSource(IList),否则用 Items
        private IList? EnsureMutableList()
        {
            if (ItemsSource is IList l && !l.IsReadOnly) return l;
            if (ItemsSource == null) return Items;
            return null;
        }

        private void Reject(string text, string reason) =>
            RaiseEvent(new TagRejectedEventArgs(TagRejectedEvent, this, text, reason));
    }

    // ---------- 事件参数与委托 ----------

    public delegate void TagChangingEventHandler(object sender, TagChangingEventArgs e);
    public delegate void TagChangedEventHandler(object sender, TagChangedEventArgs e);
    public delegate void TagRejectedEventHandler(object sender, TagRejectedEventArgs e);

    /// <summary>可取消的"将要变更"事件参数。</summary>
    public class TagChangingEventArgs : RoutedEventArgs
    {
        public string Text { get; }
        public bool Cancel { get; set; }
        public TagChangingEventArgs(RoutedEvent e, object src, string text) : base(e, src)
            => Text = text;
    }

    /// <summary>"已变更 / 被点击"事件参数。</summary>
    public class TagChangedEventArgs : RoutedEventArgs
    {
        public string Text { get; }
        public TagChangedEventArgs(RoutedEvent e, object src, string text) : base(e, src)
            => Text = text;
    }

    /// <summary>校验失败事件参数。</summary>
    public class TagRejectedEventArgs : RoutedEventArgs
    {
        public string Text { get; }
        public string Reason { get; }
        public TagRejectedEventArgs(RoutedEvent e, object src, string text, string reason)
            : base(e, src) { Text = text; Reason = reason; }
    }
}

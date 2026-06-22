using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTUI.Controls
{
    /// <summary>
    /// 单个标签项。负责显示文字、悬停出现删除按钮、双击/右键进入重命名。
    /// 所有操作通过路由事件冒泡给外层 JTTagControl 统一处理。
    /// </summary>
    [TemplatePart(Name = PartTextBox, Type = typeof(JTTextBox))]
    public class JTTag : Control
    {
        private const string PartTextBox = "PART_TextBox";
        private JTTextBox? _textBox;

        static JTTag()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTTag),
                new FrameworkPropertyMetadata(typeof(JTTag)));
        }

        /// <summary>标签文本。</summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text), typeof(string), typeof(JTTag),
                new FrameworkPropertyMetadata(string.Empty));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>圆角,默认胶囊形(由样式给默认值)。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JTTag),
                new FrameworkPropertyMetadata(new CornerRadius(3)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>是否处于重命名编辑态(供模板触发器使用)。</summary>
        private static readonly DependencyPropertyKey IsEditingPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsEditing), typeof(bool), typeof(JTTag),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsEditingProperty =
            IsEditingPropertyKey.DependencyProperty;

        public bool IsEditing
        {
            get => (bool)GetValue(IsEditingProperty);
            private set => SetValue(IsEditingPropertyKey, value);
        }

        // ---------- 路由事件:全部冒泡给 JTTagControl ----------

        public static readonly RoutedEvent ClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(Click), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(JTTag));
        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public static readonly RoutedEvent DeleteRequestedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(DeleteRequested), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(JTTag));
        public event RoutedEventHandler DeleteRequested
        {
            add => AddHandler(DeleteRequestedEvent, value);
            remove => RemoveHandler(DeleteRequestedEvent, value);
        }

        /// <summary>重命名提交事件,携带新文本(未校验)。</summary>
        public static readonly RoutedEvent RenameCommittedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(RenameCommitted), RoutingStrategy.Bubble,
                typeof(TagRenameEventHandler), typeof(JTTag));
        public event TagRenameEventHandler RenameCommitted
        {
            add => AddHandler(RenameCommittedEvent, value);
            remove => RemoveHandler(RenameCommittedEvent, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_textBox != null)
            {
                _textBox.KeyDown -= OnEditKeyDown;
                _textBox.LostKeyboardFocus -= OnEditLostFocus;
            }

            _textBox = GetTemplateChild(PartTextBox) as JTTextBox;

            if (_textBox != null)
            {
                _textBox.KeyDown += OnEditKeyDown;
                _textBox.LostKeyboardFocus += OnEditLostFocus;
            }

            // 删除按钮(模板里命名为 PART_Delete)
            if (GetTemplateChild("PART_Delete") is UIElement del)
                del.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    RaiseEvent(new RoutedEventArgs(DeleteRequestedEvent, this));
                };
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (IsEditing) return;
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            BeginEdit();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            BeginEdit();
            e.Handled = true;
        }

        /// <summary>进入重命名编辑态(也可由外部主动调用)。</summary>
        public void BeginEdit()
        {
            if (_textBox == null) return;
            IsEditing = true;
            _textBox.Text = Text;
            _textBox.Focus();
            _textBox.SelectAll();
        }

        private void CommitEdit()
        {
            if (_textBox == null) return;
            string newText = _textBox.Text;
            IsEditing = false;
            // 把新文本交给外层校验/去重/写回,这里不直接改 Text
            RaiseEvent(new TagRenameEventArgs(RenameCommittedEvent, this, Text, newText));
        }

        private void CancelEdit() => IsEditing = false;

        private void OnEditKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitEdit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { CancelEdit(); e.Handled = true; }
        }

        private void OnEditLostFocus(object? sender, RoutedEventArgs e)
        {
            if (IsEditing) CommitEdit();
        }
    }

    // 重命名事件参数
    public delegate void TagRenameEventHandler(object sender, TagRenameEventArgs e);

    public class TagRenameEventArgs : RoutedEventArgs
    {
        public string OldText { get; }
        public string NewText { get; }
        /// <summary>仅用于"将要重命名"(TagRenaming)事件:设为 true 可取消本次重命名。</summary>
        public bool Cancel { get; set; }

        public TagRenameEventArgs(RoutedEvent e, object source, string oldText, string newText)
            : base(e, source)
        {
            OldText = oldText;
            NewText = newText;
        }
    }

}

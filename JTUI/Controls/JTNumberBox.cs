using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTUI.Controls
{
    /// <summary>
    /// JTUI 数字输入框(Blender 风格)。
    /// 交互:鼠标滚轮加减、在框内按住左右拖动改值、单击进入键盘编辑。
    /// 布局:Label 靠左,数值 + 单位靠右;悬停时左右出现精简小箭头提示。
    /// 无原生上下箭头按钮。
    /// 修饰键:Ctrl 拖/滚为粗调(×10),Shift 为精调(×0.1)。
    /// </summary>
    [TemplatePart(Name = PartTextBox, Type = typeof(JTTextBox))]
    public class JTNumberBox : Control
    {
        private const string PartTextBox = "PART_TextBox";

        private JTTextBox? _textBox;
        private bool _editing;          // 是否处于键盘编辑态
        private bool _dragging;         // 是否正在拖拽改值
        private bool _dragMoved;        // 拖拽是否已超过阈值
        private Point _dragStartScreen; // 拖拽起点(屏幕坐标)
        private double _dragStartValue; // 拖拽起点时的值
        private double _dragAccumX;     // 累积横向位移(用于光标 wrap 无限拖动)

        private const double DragThreshold = 4.0;

        static JTNumberBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTNumberBox),
                new FrameworkPropertyMetadata(typeof(JTNumberBox)));
        }

        // ---------- 依赖属性 ----------

        /// <summary>当前数值。</summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value), typeof(double), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(
                    0.0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged, CoerceValue));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>最小值,默认负无穷(不限制)。</summary>
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum), typeof(double), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(double.NegativeInfinity, OnRangeChanged));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>最大值,默认正无穷(不限制)。</summary>
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum), typeof(double), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(double.PositiveInfinity, OnRangeChanged));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>滚轮 / 拖拽的基础步进,默认 1。</summary>
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(
                nameof(Step), typeof(double), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(1.0));

        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        /// <summary>拖拽灵敏度:每像素改变多少个 Step。默认 0.1(拖 10px ≈ 1 个 Step)。</summary>
        public static readonly DependencyProperty DragSensitivityProperty =
            DependencyProperty.Register(
                nameof(DragSensitivity), typeof(double), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(0.1));

        public double DragSensitivity
        {
            get => (double)GetValue(DragSensitivityProperty);
            set => SetValue(DragSensitivityProperty, value);
        }

        /// <summary>小数位数,用于显示格式化与四舍五入。默认 2。</summary>
        public static readonly DependencyProperty DecimalsProperty =
            DependencyProperty.Register(
                nameof(Decimals), typeof(int), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(2, OnDisplayChanged));

        public int Decimals
        {
            get => (int)GetValue(DecimalsProperty);
            set => SetValue(DecimalsProperty, value);
        }

        /// <summary>显示在最左侧的标签(如 "X:"),可为空。</summary>
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label), typeof(string), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>数值单位后缀(如 "px"、"°"),显示在数值右侧,可为空。</summary>
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit), typeof(string), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(string.Empty, OnDisplayChanged));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        /// <summary>圆角,默认 0(与库内其他控件一致)。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        // 内部只读属性:供模板里右侧 TextBlock 绑定(数值 + 单位,不含 Label)
        private static readonly DependencyPropertyKey ValueDisplayTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(ValueDisplayText), typeof(string), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty ValueDisplayTextProperty =
            ValueDisplayTextPropertyKey.DependencyProperty;

        public string ValueDisplayText
        {
            get => (string)GetValue(ValueDisplayTextProperty);
            private set => SetValue(ValueDisplayTextPropertyKey, value);
        }

        /// <summary>是否处于键盘编辑态(供模板触发器控制显示/隐藏)。</summary>
        private static readonly DependencyPropertyKey IsEditingPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsEditing), typeof(bool), typeof(JTNumberBox),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsEditingProperty =
            IsEditingPropertyKey.DependencyProperty;

        public bool IsEditing
        {
            get => (bool)GetValue(IsEditingProperty);
            private set => SetValue(IsEditingPropertyKey, value);
        }

        /// <summary>数值变化事件。</summary>
        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ValueChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<double>), typeof(JTNumberBox));

        public event RoutedPropertyChangedEventHandler<double> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        // ---------- 属性回调 ----------

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var box = (JTNumberBox)d;
            double v = (double)baseValue;
            if (v < box.Minimum) v = box.Minimum;
            if (v > box.Maximum) v = box.Maximum;
            if (!double.IsInfinity(v) && !double.IsNaN(v))
                v = Math.Round(v, Math.Max(0, box.Decimals));
            return v;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (JTNumberBox)d;
            box.UpdateDisplayText();
            box.RaiseEvent(new RoutedPropertyChangedEventArgs<double>(
                (double)e.OldValue, (double)e.NewValue, ValueChangedEvent));
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => d.CoerceValue(ValueProperty);

        private static void OnDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((JTNumberBox)d).UpdateDisplayText();

        private void UpdateDisplayText()
        {
            string num = Value.ToString("F" + Math.Max(0, Decimals), CultureInfo.CurrentCulture);
            ValueDisplayText = $" {num} {Unit}";
        }

        // ---------- 模板 ----------

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

            UpdateDisplayText();
        }

        // ---------- 滚轮 ----------

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_editing) return;

            double step = EffectiveStep();
            int notches = e.Delta / 120;
            if (notches == 0) notches = e.Delta > 0 ? 1 : -1;
            Value += notches * step;
            e.Handled = true;
        }

        // ---------- 拖拽 ----------

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_editing) return;

            _dragging = true;
            _dragMoved = false;
            _dragStartValue = Value;
            _dragStartScreen = PointToScreen(e.GetPosition(this));
            _dragAccumX = 0;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            Point cur = PointToScreen(e.GetPosition(this));
            double dx = cur.X - _dragStartScreen.X;

            if (!_dragMoved)
            {
                if (Math.Abs(dx) < DragThreshold) return;
                // 超过阈值,正式进入拖拽:隐藏光标
                _dragMoved = true;
                Mouse.OverrideCursor = Cursors.None;
            }

            _dragAccumX += dx;

            double step = EffectiveStep();
            Value = _dragStartValue + _dragAccumX * DragSensitivity * step;

            // 光标 wrap:把指针拨回起点,实现无限横向拖动
            SetCursorPos((int)_dragStartScreen.X, (int)_dragStartScreen.Y);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging) return;

            _dragging = false;
            ReleaseMouseCapture();
            Mouse.OverrideCursor = null;

            if (!_dragMoved)
            {
                // 没有移动 → 当作点击,进入键盘编辑态
                EnterEditMode();
            }
            e.Handled = true;
        }

        // ---------- 编辑态 ----------

        private void EnterEditMode()
        {
            if (_textBox == null) return;
            _editing = true;
            IsEditing = true;
            _textBox.Text = Value.ToString(
                "F" + Math.Max(0, Decimals), CultureInfo.CurrentCulture);
            _textBox.Focus();
            _textBox.SelectAll();
        }

        private void CommitEdit()
        {
            if (_textBox == null) return;
            if (double.TryParse(_textBox.Text,
                    NumberStyles.Any, CultureInfo.CurrentCulture, out double v) ||
                double.TryParse(_textBox.Text,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out v))
            {
                Value = v;  // CoerceValue 会自动夹取范围与四舍五入
            }
            ExitEditMode();
        }

        private void CancelEdit() => ExitEditMode();

        private void ExitEditMode()
        {
            _editing = false;
            IsEditing = false;
            UpdateDisplayText();
            Focus(); // 焦点收回控件本身,以便继续接收滚轮
        }

        private void OnEditKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private void OnEditLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_editing) CommitEdit();
        }

        // ---------- 工具 ----------

        private double EffectiveStep()
        {
            double step = Step;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) step *= 10;   // 粗调
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) step *= 0.1;    // 精调
            return step;
        }

        // Win32:把光标移回固定点,实现无限拖动
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
    }
}

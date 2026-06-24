using System.Windows;
using System.Windows.Controls.Primitives;

namespace JTUI.Controls
{
    /// <summary>
    /// JTUI 图标开关:外观与 JTIconButton 一致,但具备选中态。
    /// 未选中=透明背景,悬停高亮;选中后显示底色(JT.IconToggle.CheckedBackground)。
    /// 继承 ToggleButton,直接复用 IsChecked / Checked / Unchecked。
    /// </summary>
    public class JTIconToggleButton : ToggleButton
    {
        static JTIconToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTIconToggleButton),
                new FrameworkPropertyMetadata(typeof(JTIconToggleButton)));
        }

        /// <summary>圆角,默认 4(与 JTIconButton 一致)。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JTIconToggleButton),
                new FrameworkPropertyMetadata(new CornerRadius(4)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}

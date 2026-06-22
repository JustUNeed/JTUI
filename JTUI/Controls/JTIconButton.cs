using System.Windows;
using System.Windows.Controls;

namespace JTUI.Controls
{
    /// <summary>JTUI 图标按钮:无背景,悬停高亮,内容为图标字符或任意元素。</summary>
    public class JTIconButton : Button
    {
        static JTIconButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTIconButton),
                new FrameworkPropertyMetadata(typeof(JTIconButton)));
        }

        /// <summary>圆角,默认 4。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JTIconButton),
                new FrameworkPropertyMetadata(new CornerRadius(4)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}

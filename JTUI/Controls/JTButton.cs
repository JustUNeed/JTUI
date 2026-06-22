using System.Windows;
using System.Windows.Controls;

namespace JTUI.Controls
{
    /// <summary>JTUI 基础按钮:默认无边框、无圆角,圆角可由 CornerRadius 自行设置。</summary>
    public class JTButton : Button
    {
        static JTButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTButton),
                new FrameworkPropertyMetadata(typeof(JTButton)));
        }

        /// <summary>按钮圆角,默认 0(直角)。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(JTButton),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}

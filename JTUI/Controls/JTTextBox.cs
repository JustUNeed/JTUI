using System.Windows;
using System.Windows.Controls;

namespace JTUI.Controls
{
    /// <summary>JTUI 文本输入框:默认浅色边线,聚焦时边框高亮,无系统蓝色焦点框。</summary>
    public class JTTextBox : TextBox
    {
        static JTTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTTextBox),
                new FrameworkPropertyMetadata(typeof(JTTextBox)));
        }

        public JTTextBox()
        {
            ContextMenu = null;
            // 加载后再清一次,防止内部默认菜单回填
            Loaded += (_, _) => ContextMenu = null;
        }


        /// <summary>占位提示文字(无内容且未聚焦时显示)。</summary>
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder), typeof(string), typeof(JTTextBox),
                new FrameworkPropertyMetadata(string.Empty));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        /// <summary>圆角,默认 0。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JTTextBox),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}

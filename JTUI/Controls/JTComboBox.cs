using System.Windows;
using System.Windows.Controls;

namespace JTUI.Controls
{
    /// <summary>
    /// JTUI 下拉单选菜单。仅重写视觉风格,完整保留原生 ComboBox 的全部能力
    /// (ItemsSource / SelectedItem / SelectedValuePath / 数据模板 / 键盘操作等)。
    /// </summary>
    public class JTComboBox : ComboBox
    {
        static JTComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTComboBox),
                new FrameworkPropertyMetadata(typeof(JTComboBox)));
        }

        /// <summary>圆角,默认 0(与库内其他控件一致)。</summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius), typeof(CornerRadius), typeof(JTComboBox),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>占位提示文字(未选中任何项时显示)。</summary>
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder), typeof(string), typeof(JTComboBox),
                new FrameworkPropertyMetadata(string.Empty));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }
    }
}

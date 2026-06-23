using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JTUI.Controls
{
    /// <summary>
    /// 通用删除角标。继承 Button,Click 照常冒泡,
    /// 供 JTImageGrid / JTFileGrid / JTFolderBin 等网格的统一删除接管使用。
    /// 默认外观:右上角圆形半透明底 + 删除图标,悬停时变红。
    /// </summary>
    public class JTDeleteBadge : Button
    {
        static JTDeleteBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTDeleteBadge),
                new FrameworkPropertyMetadata(typeof(JTDeleteBadge)));
        }

        /// <summary>角标圆形直径,默认 20。</summary>
        public static readonly DependencyProperty BadgeSizeProperty =
            DependencyProperty.Register(nameof(BadgeSize), typeof(double),
                typeof(JTDeleteBadge), new FrameworkPropertyMetadata(20.0));
        public double BadgeSize
        {
            get => (double)GetValue(BadgeSizeProperty);
            set => SetValue(BadgeSizeProperty, value);
        }

        /// <summary>常态底色,默认半透明黑。</summary>
        public static readonly DependencyProperty BadgeBackgroundProperty =
            DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush),
                typeof(JTDeleteBadge),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0))));
        public Brush BadgeBackground
        {
            get => (Brush)GetValue(BadgeBackgroundProperty);
            set => SetValue(BadgeBackgroundProperty, value);
        }

        /// <summary>悬停底色,默认红。</summary>
        public static readonly DependencyProperty HoverBackgroundProperty =
            DependencyProperty.Register(nameof(HoverBackground), typeof(Brush),
                typeof(JTDeleteBadge),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xE0, 0x3A, 0x3A))));
        public Brush HoverBackground
        {
            get => (Brush)GetValue(HoverBackgroundProperty);
            set => SetValue(HoverBackgroundProperty, value);
        }

        /// <summary>图标字形(Segoe MDL2 Assets),默认 E711(删除)。</summary>
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string),
                typeof(JTDeleteBadge), new FrameworkPropertyMetadata("\uE711"));
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JTUI.Controls.ImageGrid
{
    /// <summary>把边长(double)转成正方形 Size,供 VirtualizingWrapPanel.ItemSize 使用。</summary>
    public class SquareSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = value is double d ? d : 0;
            return new Size(v, v);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

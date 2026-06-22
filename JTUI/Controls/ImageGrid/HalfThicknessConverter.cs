using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JTUI.Controls.ImageGrid
{
    /// <summary>把间隔值(double)转成四周各一半的 Thickness,使相邻项合计为完整间隔。</summary>
    public class HalfThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = value is double d ? d : 0;
            return new Thickness(v / 2);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

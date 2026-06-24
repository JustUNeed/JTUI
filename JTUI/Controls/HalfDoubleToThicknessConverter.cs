using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JTUI.Controls
{
    /// <summary>把一个 double(间距)转成四边各为其一半的 Thickness,用于格子 Margin。</summary>
    public sealed class HalfDoubleToThicknessConverter : IValueConverter
    {
        public static readonly HalfDoubleToThicknessConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double d = value is double v ? v : 0;
            return new Thickness(d / 2);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

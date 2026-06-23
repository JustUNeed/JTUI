using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JTUI.Controls.FolderBin
{
    /// <summary>把 (width, height) 两个 double 合成 Size,供 VirtualizingWrapPanel.ItemSize 用。</summary>
    public sealed class SizeFromWidthHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double w = values.Length > 0 && values[0] is double a ? a : 0;
            double h = values.Length > 1 && values[1] is double b ? b : 0;
            return new Size(w, h);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

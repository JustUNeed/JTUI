// JTUI/Controls/HalfDoubleConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace JTUI.Controls
{
    public class HalfDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is double d ? d / 2.0 : 0.0;
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => Binding.DoNothing;
    }
}

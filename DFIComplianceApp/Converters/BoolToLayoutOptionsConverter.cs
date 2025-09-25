using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace DFIComplianceApp.Converters
{
    public class BoolToLayoutOptionsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isIncoming = (bool)value;
            return isIncoming ? LayoutOptions.Start : LayoutOptions.End;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

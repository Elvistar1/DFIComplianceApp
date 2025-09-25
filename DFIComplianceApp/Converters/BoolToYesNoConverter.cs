using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Converters
{
    public class BoolToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "Yes" : "No";
            return "No";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return s.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            return false;
        }
    }
}

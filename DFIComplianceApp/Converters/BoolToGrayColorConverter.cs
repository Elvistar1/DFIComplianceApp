// In your project, create BoolToGrayColorConverter.cs:

using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Converters
{
    public class BoolToGrayColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Colors.Gray;
            return Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

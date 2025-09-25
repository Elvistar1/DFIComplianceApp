using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace DFIComplianceApp.Converters
{
    public class OverdueColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int overdue && overdue > 0)
                return Colors.Red;
            return Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

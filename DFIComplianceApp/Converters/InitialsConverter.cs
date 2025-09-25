// File: Converters/InitialsConverter.cs
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Converters
{
    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string name || string.IsNullOrWhiteSpace(name))
                return "?";

            var parts = name.Split(' ');
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpper();

            return (parts[0][0].ToString() + parts[^1][0]).ToUpper();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

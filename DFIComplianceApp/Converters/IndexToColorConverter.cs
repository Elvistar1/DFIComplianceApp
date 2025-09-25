using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Converters
{
    public class IndexToColorConverter : IValueConverter
    {
        // Alternate colors: light gray and white
        private static readonly Color EvenColor = Color.FromArgb("#ffffff");
        private static readonly Color OddColor = Color.FromArgb("#f1f1f1");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return (index % 2 == 0) ? EvenColor : OddColor;
            }

            return EvenColor; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

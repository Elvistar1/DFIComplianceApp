using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace DFIComplianceApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        // Bubble background colors
        public Color TrueColor { get; set; } = Color.FromArgb("#C8E6C9");   // greenish
        public Color FalseColor { get; set; } = Color.FromArgb("#EEEEEE");  // gray

        // Text colors
        public Color TrueTextColor { get; set; } = Colors.Black;
        public Color FalseTextColor { get; set; } = Colors.White;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                // If "Text" is passed as parameter → return text color
                if (parameter?.ToString() == "Text")
                    return b ? TrueTextColor : FalseTextColor;

                // Otherwise → return background color
                return b ? TrueColor : FalseColor;
            }

            return FalseColor; // fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

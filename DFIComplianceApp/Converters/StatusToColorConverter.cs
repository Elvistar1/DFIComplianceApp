using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Converters
{
    // Converters/StatusToColorConverter.cs
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value?.ToString()) switch
            {
                "Queued" => Colors.Gray,
                "Completed" => Colors.Green,
                "Approved" => Colors.Blue,
                "Flagged" => Colors.OrangeRed,
                "Failed" => Colors.Red,
                _ => Colors.Black
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

}

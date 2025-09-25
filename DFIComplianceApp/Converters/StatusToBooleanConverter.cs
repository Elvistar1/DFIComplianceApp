using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Converters
{
    /// <summary>
    /// Returns *false* when the record is already in <paramref name="TargetStatus"/>,
    /// otherwise *true* – handy for enabling / disabling buttons.
    /// </summary>
    public sealed class StatusToBooleanConverter : IValueConverter
    {
        public string TargetStatus { get; set; } = "Approved";
        public bool InvertResult { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? "";
            bool same = status.Equals(TargetStatus, StringComparison.OrdinalIgnoreCase);

            return InvertResult ? same : !same;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

using System.Globalization;

namespace DFIComplianceApp.Converters
{
    public class RiskLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "High" => Colors.Red,
                "Medium" => Colors.Orange,
                "Low" => Colors.Green,
                _ => Colors.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
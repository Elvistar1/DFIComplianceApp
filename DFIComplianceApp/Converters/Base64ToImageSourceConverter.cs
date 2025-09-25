using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using System.IO;

namespace DFIComplianceApp.Converters
{
    public class Base64ToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            string pathOrBase64 = value.ToString();

            // Case 1: Local file path
            if (File.Exists(pathOrBase64))
                return ImageSource.FromFile(pathOrBase64);

            // Case 2: URL
            if (pathOrBase64.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return ImageSource.FromUri(new Uri(pathOrBase64));

            // Case 3: Base64
            try
            {
                byte[] bytes = System.Convert.FromBase64String(pathOrBase64);
                return ImageSource.FromStream(() => new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}

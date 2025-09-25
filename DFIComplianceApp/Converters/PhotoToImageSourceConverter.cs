using System;
using System.Globalization;
using System.IO;
using Microsoft.Maui.Controls;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Converters
{
    public class PhotoToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not InspectionPhoto photo)
                return "image_not_found.png";

            try
            {
                // ✅ 1. Local file path (offline first)
                if (!string.IsNullOrEmpty(photo.LocalPath) && File.Exists(photo.LocalPath))
                {
                    return ImageSource.FromFile(photo.LocalPath);
                }

                // ✅ 2. Firebase download URL
                if (!string.IsNullOrWhiteSpace(photo.DownloadUrl) &&
                    Uri.TryCreate(photo.DownloadUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return ImageSource.FromUri(uri);
                }

                // ✅ 3. Base64 encoded string (legacy fallback)
                if (!string.IsNullOrWhiteSpace(photo.PhotoBase64))
                {
                    try
                    {
                        byte[] bytes = System.Convert.FromBase64String(photo.PhotoBase64);
                        return ImageSource.FromStream(() => new MemoryStream(bytes));
                    }
                    catch
                    {
                        // Ignore invalid base64
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PhotoConverter] Failed to load image: {ex.Message}");
            }

            // ✅ 4. Fallback placeholder image
            return "image_not_found.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

using System;
using System.IO;
using Microsoft.Maui.Controls;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Views
{
    public partial class PhotoPreviewPage : ContentPage
    {
        public PhotoPreviewPage(InspectionPhoto photo)
        {
            InitializeComponent();
            LoadImage(photo);
        }

        private void LoadImage(InspectionPhoto photo)
        {
            if (photo == null)
            {
                FullImage.Source = "image_not_found.png"; // fallback
                return;
            }

            try
            {
                // ✅ Case 1: Local File Path
                if (!string.IsNullOrEmpty(photo.LocalPath) && File.Exists(photo.LocalPath))
                {
                    FullImage.Source = ImageSource.FromFile(photo.LocalPath);
                    return;
                }

                // ✅ Case 2: Remote URL
                if (!string.IsNullOrEmpty(photo.DownloadUrl) &&
                    Uri.TryCreate(photo.DownloadUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    FullImage.Source = ImageSource.FromUri(uri);
                    return;
                }

                // ✅ Case 3: Base64
                if (!string.IsNullOrEmpty(photo.PhotoBase64))
                {
                    byte[] bytes = Convert.FromBase64String(photo.PhotoBase64);
                    FullImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                    return;
                }

                // Fallback
                FullImage.Source = "image_not_found.png";
            }
            catch
            {
                FullImage.Source = "image_not_found.png";
            }
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Running)
            {
                FullImage.Scale *= e.Scale;
            }
            else if (e.Status == GestureStatus.Completed)
            {
                if (FullImage.Scale < 1)
                    FullImage.Scale = 1;
            }
        }
    }
}

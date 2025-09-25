using Microsoft.Maui.Controls;
using SQLite;
using System;
using System.IO;

namespace DFIComplianceApp.Models
{
    public class InspectionPhoto
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid InspectionAnswerId { get; set; }      // FK to the answer row
        public string LocalPath { get; set; } = "";
        public DateTime UploadedAt { get; set; }

        // 🔹 Firebase fields
        public string FileName { get; set; } = "";
        public string? PhotoBase64 { get; set; }          // (Legacy support)
        public string DownloadUrl { get; set; } = "";     // ✅ Firebase Storage link

        // 🔹 Sync metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDirty { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        // ✅ Computed property for displaying the photo in XAML
        public ImageSource DisplaySource
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath))
                        return ImageSource.FromFile(LocalPath);

                    if (!string.IsNullOrEmpty(PhotoBase64))
                    {
                        byte[] bytes = Convert.FromBase64String(PhotoBase64);
                        return ImageSource.FromStream(() => new MemoryStream(bytes));
                    }

                    if (!string.IsNullOrEmpty(DownloadUrl))
                        return ImageSource.FromUri(new Uri(DownloadUrl));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InspectionPhoto.DisplaySource] Error: {ex.Message}");
                }

                return null;
            }
        }
    }
}

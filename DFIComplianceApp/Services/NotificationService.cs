using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace DFIComplianceApp.Services
{
    public interface INotificationService
    {
        Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short);
        Task ShowAlertAsync(string title, string message, string cancel = "OK");
    }

    public class NotificationService : INotificationService
    {
        private readonly Page _page;

        public NotificationService(Page page)
        {
            _page = page;
        }

        public async Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short)
        {
            try
            {
                var toast = Toast.Make(message, duration);
                await toast.Show();
            }
            catch
            {
                await ShowAlertAsync("Notification", message);
            }
        }

        public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
        {
            if (_page != null)
                await _page.DisplayAlert(title, message, cancel);
        }
    }
}

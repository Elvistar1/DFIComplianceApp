using DFIComplianceApp.Models;
using DFIComplianceApp.Services;

namespace DFIComplianceApp.Views
{
    public partial class EmailQueuePage : ContentPage
    {
        private readonly EmailReceiverService _emailReceiverService;
        private readonly IAppDatabase _db;

        public EmailQueuePage()
        {
            InitializeComponent();
            _emailReceiverService = App.Services.GetRequiredService<EmailReceiverService>();
            _db = App.Services.GetRequiredService<IAppDatabase>();

            LoadEmailsAsync();
            UpdateBadgeAsync(); // load badge on page open
        }

        private async Task LoadEmailsAsync()
        {
            try
            {
                var pendingEmails = await _db.GetPendingOutboxAsync();
                EmailListView.ItemsSource = pendingEmails;

                await UpdateBadgeAsync(); // update badge when emails are loaded
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load email queue: {ex.Message}", "OK");
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await _emailReceiverService.CheckNewEmailsAsync();
            await LoadEmailsAsync();
        }

        private async void OnEmailSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is OutboxMessage selectedEmail)
            {
                await Navigation.PushAsync(new EmailDetailOutboxPage(selectedEmail));
                EmailListView.SelectedItem = null;
            }
        }


        private async Task UpdateBadgeAsync()
        {
            try
            {
                // Count pending/unread emails
                var unreadCount = (await _db.GetPendingOutboxAsync()).Count;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (unreadCount > 0)
                    {
                        EmailBadgeLabel.Text = unreadCount.ToString();
                        EmailBadgeFrame.IsVisible = true;
                    }
                    else
                    {
                        EmailBadgeFrame.IsVisible = false;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Badge update failed: {ex.Message}");
            }
        }
    }
}

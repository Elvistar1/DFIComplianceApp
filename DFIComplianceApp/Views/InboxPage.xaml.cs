using DFIComplianceApp.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyEmailMessage = DFIComplianceApp.Models.EmailMessage;
using DFIComplianceApp.Views;

namespace DFIComplianceApp.Views
{
    public partial class InboxPage : ContentPage
    {
        private readonly IAppDatabase _db;
        private ObservableCollection<MyEmailMessage> _emails = new();

        public InboxPage()
        {
            InitializeComponent();
            _db = App.Services.GetRequiredService<IAppDatabase>();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = LoadInboxAsync();
        }

        private async Task LoadInboxAsync()
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            EmailsCollectionView.IsVisible = false;
            NoEmailsLabel.IsVisible = false;

            // 1️⃣ Load local emails first
            await LoadEmailsAsync();

            // 2️⃣ Try fetching from Gmail, but don't crash if offline
            try
            {
                await FetchLatestEmailsFromGmailAsync(20);
                await LoadEmailsAsync(); // Refresh local DB after fetch
            }
            catch (Exception ex)
            {
                // Could log error or show a toast: "Offline mode, showing cached emails"
                Console.WriteLine("Could not fetch Gmail: " + ex.Message);
            }

            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            EmailsCollectionView.IsVisible = _emails.Count > 0;
            NoEmailsLabel.IsVisible = _emails.Count == 0;
        }

        private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchAndSort();
        }

        private void OnSortPickerChanged(object sender, EventArgs e)
        {
            ApplySearchAndSort();
        }

        private void ApplySearchAndSort()
        {
            if (_emails == null) return;

            var filtered = _emails.AsEnumerable();

            // Apply search
            string searchText = EmailSearchBar.Text?.ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(e =>
                    (e.Subject?.ToLower().Contains(searchText) ?? false) ||
                    (e.From?.ToLower().Contains(searchText) ?? false) ||
                    (e.BodyPlainText?.ToLower().Contains(searchText) ?? false));
            }

            // Apply sorting
            switch (SortPicker.SelectedIndex)
            {
                case 0: filtered = filtered.OrderByDescending(e => e.ReceivedDate); break; // Date Newest
                case 1: filtered = filtered.OrderBy(e => e.ReceivedDate); break;         // Date Oldest
                case 2: filtered = filtered.OrderBy(e => e.From); break;                 // Sender A-Z
                case 3: filtered = filtered.OrderByDescending(e => e.From); break;      // Sender Z-A
                case 4: filtered = filtered.OrderBy(e => e.Subject); break;              // Subject A-Z
                case 5: filtered = filtered.OrderByDescending(e => e.Subject); break;   // Subject Z-A
            }

            EmailsCollectionView.ItemsSource = filtered.ToList();
            NoEmailsLabel.IsVisible = !filtered.Any();
        }

        private async Task LoadEmailsAsync()
        {
            var emails = await _db.GetAllEmailMessagesAsync();
            _emails = new ObservableCollection<MyEmailMessage>(
                emails.OrderByDescending(e => e.ReceivedDate)
            );
            EmailsCollectionView.ItemsSource = _emails;
            NoEmailsLabel.IsVisible = _emails.Count == 0;
        }

        private async Task FetchLatestEmailsFromGmailAsync(int maxFetch)
        {
            string clientId = DeviceInfo.Platform == DevicePlatform.Android
                ? "508015424011-b2ls350s4rcr2k2mte1gr9rf3cv70p4o.apps.googleusercontent.com"
                : "508015424011-ras3f680mr66hsmn5g952mi2cgc0i8rb.apps.googleusercontent.com";

            string clientSecret = "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";

            var secrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            string[] scopes = { GmailService.Scope.GmailReadonly };

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                scopes,
                "user",
                CancellationToken.None,
                new FileDataStore("gmail_token_store")
            );

            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "DFIComplianceApp",
            });

            var listRequest = service.Users.Messages.List("me");
            listRequest.MaxResults = maxFetch;
            listRequest.LabelIds = "INBOX";

            var messagesResponse = await listRequest.ExecuteAsync();
            if (messagesResponse.Messages == null || messagesResponse.Messages.Count == 0)
                return;

            foreach (var msg in messagesResponse.Messages)
            {
                // Skip if email already exists in DB
                if (await _db.EmailExistsAsync(msg.Id))
                    continue;

                var message = await service.Users.Messages.Get("me", msg.Id).ExecuteAsync();
                var headers = message.Payload.Headers;
                string subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                string from = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "(Unknown Sender)";
                string dateStr = headers.FirstOrDefault(h => h.Name == "Date")?.Value;

                DateTime receivedDate;
                if (!string.IsNullOrEmpty(dateStr) &&
                    DateTimeOffset.TryParse(dateStr, out var dto))
                {
                    receivedDate = dto.UtcDateTime;
                }
                else
                {
                    receivedDate = DateTime.UtcNow;
                }

                var bodies = ExtractBodyParts(message.Payload);
                string bodyPlainText = bodies.plainText ?? "";
                string bodyHtml = bodies.html ?? "";

                await _db.SaveEmailAsync(new MyEmailMessage
                {
                    GmailMessageId = msg.Id,
                    Subject = subject,
                    From = from,
                    ReceivedDate = receivedDate,
                    BodyPlainText = bodyPlainText,
                    BodyHtml = bodyHtml
                });

            }
        }

        private async void OnRefreshRequested(object sender, EventArgs e)
        {
            try
            {
                InboxRefreshView.IsRefreshing = true;

                await FetchLatestEmailsFromGmailAsync(20);
                await LoadEmailsAsync();
            }
            finally
            {
                InboxRefreshView.IsRefreshing = false;
            }
        }

        private (string plainText, string html) ExtractBodyParts(MessagePart part)
        {
            string plainText = null;
            string html = null;

            if (part.MimeType == "text/plain" && part.Body?.Data != null)
                plainText = Base64UrlDecode(part.Body.Data);
            else if (part.MimeType == "text/html" && part.Body?.Data != null)
                html = Base64UrlDecode(part.Body.Data);
            else if (part.Parts != null && part.Parts.Count > 0)
            {
                foreach (var subPart in part.Parts)
                {
                    var result = ExtractBodyParts(subPart);
                    if (plainText == null && !string.IsNullOrEmpty(result.plainText))
                        plainText = result.plainText;
                    if (html == null && !string.IsNullOrEmpty(result.html))
                        html = result.html;
                    if (plainText != null && html != null)
                        break;
                }
            }

            return (plainText, html);
        }

        private static string Base64UrlDecode(string input)
        {
            string s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            var bytes = Convert.FromBase64String(s);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private async void OnEmailSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0)
                return;

            var selectedEmail = e.CurrentSelection[0] as MyEmailMessage;
            if (selectedEmail != null)
                await Navigation.PushAsync(new EmailDetailPage(selectedEmail));

            ((CollectionView)sender).SelectedItem = null;
        }
    }
}

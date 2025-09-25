using System;
using System.Threading.Tasks;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;
using System.Text.RegularExpressions;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Services
{
    public class EmailReceiverService
    {
        private readonly IAppDatabase _database;

        private readonly string _imapHost = "imap.gmail.com";
        private readonly int _imapPort = 993;
        private readonly bool _useSsl = true;

        // TODO: Replace with environment config, not hardcoded for production
        private readonly string _username = "gyamfikwakyeelvis004@gmail.com";
        private readonly string _password = "Angel0041&";

        public EmailReceiverService(IAppDatabase database)
        {
            _database = database;
        }

        public async Task CheckNewEmailsAsync()
        {
            using var client = new ImapClient();

            try
            {
                await client.ConnectAsync(_imapHost, _imapPort, _useSsl);
                await client.AuthenticateAsync(_username, _password);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite);

                var uids = await inbox.SearchAsync(SearchQuery.All);
                Console.WriteLine($"📬 Found {uids.Count} new emails.");

                foreach (var uid in uids)
                {
                    try
                    {
                        var message = await inbox.GetMessageAsync(uid);
                        Console.WriteLine($"📧 Processing: {message.Subject} (From: {message.From})");

                        // Get plain text or convert HTML
                        string bodyText = message.TextBody ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(bodyText) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                            bodyText = ConvertHtmlToPlainText(message.HtmlBody);

                        bodyText = bodyText?.Trim();

                        // === NEW: Save raw email instead of parsing ===
                        var emailMsg = new DFIComplianceApp.Models.EmailMessage
                        {
                            Subject = message.Subject ?? "No Subject",
                            From = message.From.ToString(),
                            ReceivedDate = message.Date.DateTime,
                            Body = bodyText,
                            IsConvertedToAppointment = false
                        };

                        await _database.InsertEmailMessageAsync(emailMsg);

                        // Optional: If you still want to parse appointment and save it separately
                        /*
                        var appointment = ParseAppointmentFromBody(bodyText);
                        if (appointment != null)
                        {
                            appointment.Subject = message.Subject ?? "No Subject";
                            appointment.Source = "Email";
                            await _database.SaveAppointmentAsync(appointment);

                            Console.WriteLine($"✅ Saved Appointment: {appointment.CompanyName} on {appointment.MeetingDate:yyyy-MM-dd}");
                        }
                        */

                        // Mark email as read
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"❌ Error processing email: {emailEx.Message}");
                    }
                }

                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 Error checking emails: {ex.Message}");
            }
        }


        public async Task<int> GetUnreadEmailCountAsync()
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_imapHost, _imapPort, _useSsl);
            await client.AuthenticateAsync(_username, _password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

            await client.DisconnectAsync(true);

            return uids.Count;
        }

        private Appointment? ParseAppointmentFromBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            var datePattern = new Regex(@"Appointment\s*Date\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase);
            var companyPattern = new Regex(@"Company\s*Name\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase);
            var contactPattern = new Regex(@"Contact\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase);
            var notesPattern = new Regex(@"Notes\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase);

            var dateMatch = datePattern.Match(body);
            var companyMatch = companyPattern.Match(body);
            var contactMatch = contactPattern.Match(body);
            var notesMatch = notesPattern.Match(body);

            if (!dateMatch.Success || !companyMatch.Success || !contactMatch.Success)
                return null;

            if (!DateTime.TryParse(dateMatch.Groups[1].Value.Trim(), out DateTime appointmentDate))
                return null;

            return new Appointment
            {
                MeetingDate = appointmentDate, // ✅ Fixed here
                CompanyName = companyMatch.Groups[1].Value.Trim(),
                Subject = contactMatch.Groups[1].Value.Trim(),
                Details = notesMatch.Success ? notesMatch.Groups[1].Value.Trim() : null,
                CreatedAt = DateTime.UtcNow
            };
        }


        private string ConvertHtmlToPlainText(string html)
        {
            // Basic HTML tag stripper (replace with HtmlAgilityPack if needed)
            return Regex.Replace(html, "<.*?>", string.Empty);
        }
    }
}

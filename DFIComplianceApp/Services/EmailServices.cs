using System.Collections.Generic;
using System.Threading.Tasks;
using DFIComplianceApp.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Maui.Networking;

namespace DFIComplianceApp.Services
{
    public sealed class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly SmtpClient _client;

        public EmailService(EmailSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings), "EmailSettings cannot be null.");

            if (string.IsNullOrWhiteSpace(settings.Host))
                throw new ArgumentException("SMTP Host is required.", nameof(settings.Host));
            if (settings.Port <= 0)
                throw new ArgumentException("SMTP Port must be a positive integer.", nameof(settings.Port));
            if (string.IsNullOrWhiteSpace(settings.Username))
                throw new ArgumentException("SMTP Username is required.", nameof(settings.Username));
            if (string.IsNullOrWhiteSpace(settings      .Password))
                throw new ArgumentException("SMTP Password is required.", nameof(settings.Password));
            if (string.IsNullOrWhiteSpace(settings.From))
                throw new ArgumentException("SMTP From address is required.", nameof(settings.From));

            _settings = settings;
            _client = new SmtpClient();
        }

        private async Task EnsureConnectedAsync()
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable
                );

                if (!string.IsNullOrEmpty(_settings.Username))
                {
                    await _client.AuthenticateAsync(_settings.Username, _settings.Password);
                }
            }
        }

        public async Task SendAsync(
            string to,
            string subject,
            string body,
            bool isHtml = false,
            IEnumerable<string>? cc = null,
            IEnumerable<string>? bcc = null,
            IEnumerable<EmailAttachment>? attachments = null)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "No Internet",
                    "Please check your network connection before sending email.",
                    "OK"
                );
                return;
            }

            int maxAttempts = 3;
            int delayMs = 2000;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(_settings.DisplayName ?? _settings.Username, _settings.From ?? _settings.Username));
                    message.To.Add(MailboxAddress.Parse(to));

                    if (cc != null)
                        foreach (var c in cc) message.Cc.Add(MailboxAddress.Parse(c));
                    if (bcc != null)
                        foreach (var b in bcc) message.Bcc.Add(MailboxAddress.Parse(b));

                    message.Subject = subject;

                    var builder = new BodyBuilder
                    {
                        HtmlBody = isHtml ? body : null,
                        TextBody = !isHtml ? body : null
                    };

                    if (attachments != null)
                        foreach (var att in attachments)
                            builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.MimeType));

                    message.Body = builder.ToMessageBody();

                    // Ensure connection is fresh
                    if (!_client.IsConnected)
                    {
                        await EnsureConnectedAsync();
                    }

                    await _client.SendAsync(message);
                    return; // Success
                }
                catch (Exception ex)
                {
                    // Disconnect and recreate client on error
                    if (_client.IsConnected)
                    {
                        await _client.DisconnectAsync(true);
                    }
                    // Optionally recreate the client if needed
                    // _client.Dispose();
                    // _client = new SmtpClient();

                    if (attempt == maxAttempts)
                    {
                        await Application.Current.MainPage.DisplayAlert("Email Send Failed", $"Error: {ex.Message}", "OK");
                    }
                    else
                    {
                        await Task.Delay(delayMs * attempt); // Exponential backoff
                    }
                }
            }
        }

        // Call this when the app shuts down to cleanly disconnect
        public async Task DisconnectAsync()
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync(true);
        }
    }
}

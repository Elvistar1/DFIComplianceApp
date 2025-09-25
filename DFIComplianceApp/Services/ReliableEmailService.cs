using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Services
{
    public sealed class ReliableEmailService : IEmailService
    {
        private readonly IEmailService _smtp;
        private readonly IAppDatabase _db;

        public ReliableEmailService(IEmailService smtp, IAppDatabase db)
        {
            _smtp = smtp;
            _db = db;
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
            try
            {
                // Attempt to send immediately using SMTP service
                await _smtp.SendAsync(to, subject, body, isHtml, cc, bcc, attachments);
            }
            catch (Exception)
            {
                // On failure, fallback to storing in local message outbox
                var msg = new OutboxMessage
                {
                    To = to,
                    Subject = subject,
                    Body = body,
                    IsHtml = isHtml,
                    IsSent = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _db.SaveMessageOutboxAsync(msg);
            }
        }
    }
}

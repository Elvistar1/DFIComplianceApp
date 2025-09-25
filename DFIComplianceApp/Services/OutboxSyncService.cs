// ==========================================================
//  Services/OutboxSyncService.cs – background email flusher
// ==========================================================
using CommunityToolkit.Mvvm.Messaging;
using DFIComplianceApp.Models;
using DFIComplianceApp.Notifications;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks;
using DFIComplianceApp.Services;

namespace DFIComplianceApp.Services
{
    /// <summary>
    ///     Periodically flushes the <c>Outbox</c> table by sending any
    ///     queued messages and marking them as delivered.  When the queue
    ///     becomes empty it broadcasts an <see cref="OutboxClearedMessage"/>
    ///     so that interested views (eg. a badge on the Director dashboard)
    ///     can update immediately.
    /// </summary>
    public sealed class OutboxSyncService
    {
        private readonly IAppDatabase _db;
        private readonly IEmailService _email;
        private readonly ISmsSender _sms;

        public OutboxSyncService(IAppDatabase db,
                                 IEmailService email,
                                 ISmsSender sms)   // ◄ NEW dependency
        {
            _db = db;
            _email = email;
            _sms = sms;
        }

        /// <summary>
        /// Flush all queued messages (email + SMS).  Unsent items remain in the queue
        /// for the next pass.  When the queue is empty a notification is broadcast.
        /// </summary>
        public async Task FlushAsync()
        {
            IReadOnlyList<OutboxMessage> pending = await _db.GetPendingOutboxAsync();

            foreach (var m in pending)
            {
                try
                {
                    switch (m.Type)
                    {
                        case "Email":
                            await _email.SendAsync(m.To, m.Subject, m.Body, m.IsHtml);
                            break;

                        case "Sms":
                            // Ensure number is E.164.  Adjust the prefix logic to match your locale.
                            string e164 = m.To.StartsWith("+") ? m.To : $"+233{m.To}";
                            await _sms.SendSmsAsync(e164, m.Body);
                            break;

                        default:
                            // Unknown type — skip; you could log this.
                            continue;
                    }

                    await _db.MarkOutboxSentAsync(m.Id, DateTime.UtcNow);
                }
                catch
                {
                    // leave message unsent for next cycle
                }
            }

            // Notify listeners if queue is fully drained
            if (await _db.GetUnsentOutboxCountAsync() == 0)
                WeakReferenceMessenger.Default.Send(new OutboxClearedMessage(0));
        }
    }

}

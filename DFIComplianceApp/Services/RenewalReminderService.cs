using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Networking;
using DFIComplianceApp.Services;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Services;

public sealed class RenewalReminderService : IAsyncDisposable
{
    private readonly IAppDatabase _db;
    private readonly IEmailService _email;
    private readonly ISmsSender _sms;
    private readonly PeriodicTimer _timer;

    public RenewalReminderService(IAppDatabase db, IEmailService email, ISmsSender sms)
    {
        _db = db;
        _email = email;
        _sms = sms;

        // run check every 12 hours
        _timer = new(TimeSpan.FromHours(12));
        _ = RunAsync();

        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? s, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
            await CheckAndSendRenewalRemindersAsync();
    }

    private async Task RunAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            await CheckAndSendRenewalRemindersAsync();
        }
    }

    private async Task CheckAndSendRenewalRemindersAsync()
    {
        var now = DateTime.Today;
        var companies = await _db.GetCompaniesAsync();

        foreach (var company in companies)
        {
            DateTime lastRenewed = await _db.GetLastRenewalDateAsync(company.Id) ?? company.RegistrationDate;
            int lastRenewalYear = lastRenewed.Year;

            if ((now - lastRenewed).TotalDays >= 365)
            {
                // check if reminder already sent
                var renewalRecord = await _db.GetCompanyRenewalAsync(company.Id, lastRenewalYear);
                if (renewalRecord is null || renewalRecord.ReminderSent)
                    continue; // skip if already reminded or no record found

                string subject = $"Renewal Reminder – {company.Name}";
                string body = $"Dear {company.Occupier},\n\nYour company registration for “{company.Name}” is due for renewal.\n\nPlease renew it via the DFI Compliance app.";

                try
                {
                    await _email.SendAsync(company.Email, subject, body, false);

                    if (!string.IsNullOrEmpty(company.Contact))
                    {
                        string e164 = company.Contact.StartsWith("+") ? company.Contact : $"+233{company.Contact}";
                        await _sms.SendSmsAsync(e164, $"Your company “{company.Name}” is due for renewal. Please renew via the DFI Compliance app.");
                    }

                    await _db.MarkReminderSentAsync(company.Id, lastRenewalYear);

                    await _db.SaveAuditLogAsync(new AuditLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Role = "System",
                        Username = "System",
                        Action = $"Sent renewal reminder to {company.Name}"
                    });
                }
                catch
                {
                    // fail silently – will retry later
                }
            }
        }
    }


    public ValueTask DisposeAsync()
    {
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _timer.Dispose();
        return ValueTask.CompletedTask;
    }
}

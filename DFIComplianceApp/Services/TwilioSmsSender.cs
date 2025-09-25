// Services/TwilioSmsSender.cs
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace DFIComplianceApp.Services;

public sealed class TwilioSmsSender : ISmsSender
{
    private readonly SmsSettings _cfg;

    public TwilioSmsSender(SmsSettings cfg)
    {
        _cfg = cfg;
        TwilioClient.Init(_cfg.AccountSid, _cfg.AuthToken);
    }

    public async Task SendSmsAsync(string toPhoneE164, string body)
    {
        await MessageResource.CreateAsync(
            body: body,
            from: new PhoneNumber(_cfg.FromNumber),
            to: new PhoneNumber(toPhoneE164));
    }
}

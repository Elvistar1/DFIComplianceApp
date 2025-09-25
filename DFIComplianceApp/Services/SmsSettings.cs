// Services/SmsSettings.cs
namespace DFIComplianceApp.Services;

public sealed class SmsSettings
{
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string FromNumber { get; set; } = "";   // your Twilio +1234567890
}

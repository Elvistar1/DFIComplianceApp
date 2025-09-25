// Services/ISmsSender.cs
using System.Threading.Tasks;

namespace DFIComplianceApp.Services;

public interface ISmsSender
{
    Task SendSmsAsync(string toPhoneE164, string body);
}

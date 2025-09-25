using System.Collections.Generic;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services
{
    public interface IEmailService
    {
        Task SendAsync(
            string to,
            string subject,
            string body,
            bool isHtml = false,
            IEnumerable<string>? cc = null,
            IEnumerable<string>? bcc = null,
            IEnumerable<EmailAttachment>? attachments = null);
    }
}

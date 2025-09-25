using System.Threading.Tasks;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Services
{
    public interface IPasswordResetService
    {
        /// <summary>
        /// Sends a password reset link to the given email.
        /// </summary>
        Task<Result<bool>> SendResetLinkAsync(string email);

        /// <summary>
        /// Resets the user’s password after validating the token.
        /// </summary>
        Task<Result<bool>> SetNewPasswordAsync(string token, string newPassword);
    }
}

// Services/IAdviceService.cs
namespace DFIComplianceApp.Services
{
    public interface IAdviceService
    {
        Task<string> GetAdviceAsync(string json,
                                    CancellationToken token = default);
    }
}

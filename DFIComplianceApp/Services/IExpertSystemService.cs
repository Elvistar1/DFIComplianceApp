using System;
using System.Threading;
using System.Threading.Tasks;

namespace DFIComplianceApp.Services
{
    public interface IExpertSystemService
    {
        /// <summary>
        /// Generates a detailed inspection report using fallback logic, including legal references and recommendations.
        /// </summary>
        /// <param name="json">Checklist JSON data.</param>
        /// <param name="ct">Cancellation token for async control.</param>
        /// <returns>A complete text report as a string.</returns>
        Task<string> GetDetailedAdviceAsync(string json, CancellationToken ct);

        /// <summary>
        /// Synchronous version of generating a detailed inspection report.
        /// </summary>
        /// <param name="json">Checklist JSON data.</param>
        /// <returns>A complete text report as a string.</returns>
        string GenerateDetailedReport(string json);
    }
}

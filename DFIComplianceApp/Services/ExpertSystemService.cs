using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DFIComplianceApp.Services;

namespace DFIComplianceApp.Services
{
    public sealed class ExpertSystemService : IExpertSystemService
    {
        public Task<string> GetDetailedAdviceAsync(string json, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(GenerateDetailedReport(json));
        }

        public string GenerateDetailedReport(string json)
        {
            using var doc = JsonDocument.Parse(json);

            string natureOfWork = doc.RootElement.GetProperty("natureOfWork").GetString() ?? "Unknown";
            string premises = doc.RootElement.GetProperty("premises").GetString() ?? "Unknown Premises";

            var sb = new StringBuilder();

            sb.AppendLine("Ref No: ...................................");
            sb.AppendLine($"Date: {DateTime.Now:dd MMM yyyy}");
            sb.AppendLine();
            sb.AppendLine("The Director");
            sb.AppendLine($"{premises}");
            sb.AppendLine();
            sb.AppendLine("Dear Sir/Madam,");
            sb.AppendLine();
            sb.AppendLine($"Following our inspection of the above-mentioned premises, engaged in {natureOfWork}, we wish to bring to your attention the following observations as per the Factories, Offices and Shops Act, 1970 (Act 328):");
            sb.AppendLine();

            int nonCompliant = 0;

            if (doc.RootElement.TryGetProperty("answers", out var answersElement) && answersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var answer in answersElement.EnumerateArray())
                {
                    bool isCompliant = answer.GetProperty("compliant").GetBoolean();
                    string question = answer.GetProperty("question").GetString() ?? "[Unknown Question]";
                    string notes = answer.TryGetProperty("notes", out var notesElement) ? notesElement.GetString() ?? "" : "";

                    if (!isCompliant)
                    {
                        nonCompliant++;

                        string normalizedQuestion = question.Trim().Replace("‑", "-");

                        // Safer lookup using case-insensitive fallback:
                        var matchedKey = ExpertSystemLegalReferences.LegalReferences.Keys
                            .FirstOrDefault(k => string.Equals(k.Trim(), normalizedQuestion, StringComparison.OrdinalIgnoreCase));

                        if (matchedKey != null)
                        {
                            var legalData = ExpertSystemLegalReferences.LegalReferences[matchedKey];
                            sb.AppendLine($"• It was observed that \"{question}\" was not complied with. This is classified as {legalData.Risk} risk under {legalData.Section} of Act 328.");
                            sb.AppendLine($"  Recommendation: {legalData.Recommendation}");
                        }
                        else
                        {
                            sb.AppendLine($"• It was observed that \"{question}\" was not complied with. Section and risk level unknown.");
                            sb.AppendLine("  Recommendation: Please review this item manually.");
                        }

                        if (!string.IsNullOrWhiteSpace(notes))
                            sb.AppendLine($"  Inspector Notes: {notes}");

                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine("No checklist answers found in the provided inspection data.");
            }

            if (nonCompliant == 0)
            {
                sb.AppendLine("We are pleased to report that no non-compliance was observed during the inspection.");
            }

            sb.AppendLine();
            sb.AppendLine("We therefore strongly advise that all non-compliant issues highlighted above be addressed promptly in accordance with the law.");
            sb.AppendLine("Please ensure all necessary corrective actions are implemented and records are maintained as required.");
            sb.AppendLine();
            sb.AppendLine("Yours faithfully,");
            sb.AppendLine("...................................................");
            sb.AppendLine("Name of Inspector: ___________________");

            return sb.ToString();
        }



    }
}

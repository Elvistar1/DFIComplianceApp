using Microsoft.ML;
using System;
using System.IO;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Services
{
    public class RiskPredictionService
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _model;
        private readonly PredictionEngine<CompanyRiskInput, ModelOutput> _predictionEngine;

        public RiskPredictionService()
        {
            _mlContext = new MLContext();

            // 🔁 Load the model once
            string modelPath = Path.Combine(FileSystem.Current.AppDataDirectory, "company_risk_model.zip");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException("ML model file not found at: " + modelPath);

            using var stream = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _model = _mlContext.Model.Load(stream, out _);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<CompanyRiskInput, ModelOutput>(_model);
        }

        public string PredictRiskLevel(CompanyRiskInput input)
        {
            // ✅ Sanitize inputs to avoid unseen categories error
            input.RenewalStatus = NormalizeRenewalStatus(input.RenewalStatus);
            input.CompanyType = NormalizeCompanyType(input.CompanyType);

            var prediction = _predictionEngine.Predict(input);
            return prediction.PredictedRiskLevel;
        }

        private string NormalizeRenewalStatus(string status)
        {
            var allowed = new[] { "Renewed", "NotRenewed" };
            return Array.Exists(allowed, s => s.Equals(status, StringComparison.OrdinalIgnoreCase)) ? status : "NotRenewed";
        }

        private string NormalizeCompanyType(string type)
        {
            var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Oil & Gas Stations"] = "Oil & Gas Stations",
                ["Food Processing Companies"] = "Food Processing Companies",
                ["Wood Processing Companies"] = "Wood Processing Companies",
                ["Warehouses"] = "Warehouses",
                ["Sachet Water Production"] = "Sachet Water Production",
                ["Offices"] = "Offices",
                ["Shops"] = "Shops",
                ["Manufacturing Companies"] = "Manufacturing Companies"
            };

            return allowed.TryGetValue(type.Trim(), out var normalized) ? normalized : "Other";
        }


    }
}

using System;
using System.ComponentModel;
using System.Text.Json;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.ViewModels
{
    public sealed class ReportVM : INotifyPropertyChanged
    {
        public Guid Id { get; set; }
        public Guid InspectionId { get; set; }
        public Guid CompanyId { get; set; }
        public string Premises { get; }
        public string NatureOfWork { get; }
        public DateTime CreatedAt { get; }
        public bool CanRetry => Status != "Completed";

        public string Json { get; }
        public string ReviewerComment { get; set; } = string.Empty;
        string _status;
        string _advice;
        bool _isAnalysing;
        DateTime? _updatedAt;
        public string Status { get => _status; private set { _status = value; NotifyAll(); } }
        public string Advice { get => _advice; set { _advice = value; OnChanged(nameof(Advice)); } }
        public DateTime? UpdatedAt { get => _updatedAt; set { _updatedAt = value; OnChanged(nameof(UpdatedAt)); } }
        public bool IsAnalysing { get => _isAnalysing; private set { _isAnalysing = value; OnChanged(nameof(IsAnalysing)); OnChanged(nameof(AnalyseButtonEnabled)); } }
        public bool CanAnalyse => Status is "Queued" or "Failed";
        public bool CanApprove => Status == "Completed";
        public bool CanFlag => Status == "Completed";
        public bool AnalyseButtonEnabled => !IsAnalysing && CanAnalyse;
        public string InspectorUsername { get; }
        public DateTime? RecommendationSentAt { get; set; }
        public string? RecommendationSentBy { get; set; }
        public ReportVM(PendingAiReport row)
        {
            Id = Guid.TryParse(row.Id, out var parsed) ? parsed : Guid.Empty;
            Json = row.Json;
            _status = row.Status;
            _advice = row.Advice;
            CreatedAt = row.CreatedAt;
            _updatedAt = row.UpdatedAt;
            ReviewerComment = row.ReviewerComment ?? string.Empty;
            InspectorUsername = row.InspectorUsername ?? "(unknown)";
            var root = JsonDocument.Parse(row.Json).RootElement;
            Premises = root.GetProperty("premises").GetString() ?? "(unknown)";
            NatureOfWork = root.GetProperty("natureOfWork").GetString() ?? string.Empty;
            InspectionId = row.InspectionId;

            // FIX: Map recommendation properties from PendingAiReport
            RecommendationSentAt = row.RecommendationSentAt;
            RecommendationSentBy = row.RecommendationSentBy;
        }
        public void SetStatus(string s) => Status = s;
        public void SetAnalysing(bool flag) => IsAnalysing = flag;
        public PendingAiReport ToRow() => new()
        {
            Id = Id.ToString(), // must match the original Firebase key
            InspectionId = InspectionId, // must match the original inspection
            Json = Json,
            Status = Status,
            Advice = Advice,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt ?? DateTime.UtcNow,
            InspectorUsername = InspectorUsername,
            ReviewerComment = ReviewerComment,
            RecommendationSentAt = RecommendationSentAt,
            RecommendationSentBy = RecommendationSentBy
        };
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void NotifyAll()
        {
            OnChanged(nameof(Status)); OnChanged(nameof(CanAnalyse)); OnChanged(nameof(CanApprove)); OnChanged(nameof(CanFlag));
        }

        public bool CanSendRecommendation =>
            Status == "Completed" && !string.IsNullOrWhiteSpace(Advice) && Advice != "(Not available until approved by director)";
    }
}
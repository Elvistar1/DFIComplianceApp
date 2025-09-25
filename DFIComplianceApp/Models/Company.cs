using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DFIComplianceApp.Models
{
    public class Company : INotifyPropertyChanged
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public bool IsDirty { get; set; } = true;

        // 🔹 Core Properties
        private string _applicantNames;
        [MaxLength(300)]
        [JsonPropertyName("applicantNames")]
        public string ApplicantNames
        {
            get => _applicantNames;
            set { if (SetProperty(ref _applicantNames, value)) Touch(); }
        }

        private string? _certificateNumber;
        [MaxLength(50)]
        [JsonPropertyName("certificateNumber")]
        public string? CertificateNumber
        {
            get => _certificateNumber;
            set { if (SetProperty(ref _certificateNumber, value)) Touch(); }
        }

        private string? _fileNumber;
        [MaxLength(50)]
        [JsonPropertyName("fileNumber")]
        public string? FileNumber
        {
            get => _fileNumber;
            set { if (SetProperty(ref _fileNumber, value)) Touch(); }
        }

        private string _contact;
        [MaxLength(20)]
        [JsonPropertyName("contact")]
        public string Contact
        {
            get => _contact;
            set { if (SetProperty(ref _contact, value)) Touch(); }
        }

        private string _email;
        [MaxLength(100)]
        [JsonPropertyName("email")]
        public string Email
        {
            get => _email;
            set { if (SetProperty(ref _email, value)) Touch(); }
        }

        private string? _name;
        [MaxLength(100)]
        [JsonPropertyName("name")]
        public string? Name
        {
            get => _name;
            set { if (SetProperty(ref _name, value)) Touch(); }
        }

        private string? _location;
        [MaxLength(150)]
        [JsonPropertyName("location")]
        public string? Location
        {
            get => _location;
            set { if (SetProperty(ref _location, value)) Touch(); }
        }

        private string? _natureOfWork;
        [MaxLength(100)]
        [JsonPropertyName("natureOfWork")]
        public string? NatureOfWork
        {
            get => _natureOfWork;
            set { if (SetProperty(ref _natureOfWork, value)) Touch(); }
        }

        private string? _occupier;
        [MaxLength(100)]
        [JsonPropertyName("occupier")]
        public string? Occupier
        {
            get => _occupier;
            set { if (SetProperty(ref _occupier, value)) Touch(); }
        }
        private bool _isExpanded;
        [Ignore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private string? _registeredBy;
        [MaxLength(50)]
        [JsonPropertyName("registeredBy")]
        public string? RegisteredBy
        {
            get => _registeredBy;
            set { if (SetProperty(ref _registeredBy, value)) Touch(); }
        }

        private DateTime _registrationDate = DateTime.Now;
        [JsonPropertyName("registrationDate")]
        public DateTime RegistrationDate
        {
            get => _registrationDate;
            set { if (SetProperty(ref _registrationDate, value)) Touch(); }
        }

        private bool _isDormant = false;
        [JsonPropertyName("isDormant")]
        public bool IsDormant
        {
            get => _isDormant;
            set { if (SetProperty(ref _isDormant, value)) Touch(); }
        }

        private DateTime? _lastRenewalDate;
        [JsonPropertyName("lastRenewalDate")]
        public DateTime? LastRenewalDate
        {
            get => _lastRenewalDate;
            set { if (SetProperty(ref _lastRenewalDate, value)) Touch(); }
        }

        private string? _renewedBy;
        [JsonPropertyName("renewedBy")]
        public string? RenewedBy
        {
            get => _renewedBy;
            set { if (SetProperty(ref _renewedBy, value)) Touch(); }
        }

        private string _postalAddress;
        [MaxLength(150)]
        [JsonPropertyName("postalAddress")]
        public string PostalAddress
        {
            get => _postalAddress;
            set { if (SetProperty(ref _postalAddress, value)) Touch(); }
        }

        private int _employeesMale;
        [JsonPropertyName("employeesMale")]
        public int EmployeesMale
        {
            get => _employeesMale;
            set { if (SetProperty(ref _employeesMale, value)) Touch(); }
        }

        private int _employeesFemale;
        [JsonPropertyName("employeesFemale")]
        public int EmployeesFemale
        {
            get => _employeesFemale;
            set { if (SetProperty(ref _employeesFemale, value)) Touch(); }
        }

        private string _riskLevel;
        [JsonPropertyName("riskLevel")]
        public string RiskLevel
        {
            get => _riskLevel;
            set { if (SetProperty(ref _riskLevel, value)) Touch(); }
        }

        // Local RecordId
        public string RecordId { get; set; } = Guid.NewGuid().ToString();

        // 🔹 Computed / Ignored properties
        [Ignore] public bool IsRenewedThisYear => LastRenewalDate?.Year == DateTime.Now.Year;
        [Ignore] public DateTime? EffectiveLastRenewalDate { get; set; }
      
        [Ignore] public bool IsEditing { get; set; }
        [Ignore] public bool CanEdit { get; set; }
        [Ignore] public string StatusText { get; set; }
        [Ignore] public string ButtonText { get; set; }
        [Ignore] public string StatusColorHex { get; set; }

        [Ignore]
        public bool IsSynced
        {
            get => !IsDirty;
            set => IsDirty = !value;
        }

        // ────────── Helpers ──────────
        private void Touch()
        {
            LastModifiedUtc = DateTime.UtcNow;
            IsDirty = true;
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;
            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}

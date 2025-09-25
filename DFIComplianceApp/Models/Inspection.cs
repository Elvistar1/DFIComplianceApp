using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DFIComplianceApp.Models
{
    public class Inspection : INotifyPropertyChanged
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public bool IsDirty { get; set; } = true;
        public bool IsSynced { get; set; } = false;

        private Guid _companyId;
        public Guid CompanyId
        {
            get => _companyId;
            set { if (SetProperty(ref _companyId, value)) Touch(); }
        }

        private string _companyName = string.Empty;
        public string CompanyName
        {
            get => _companyName;
            set { if (SetProperty(ref _companyName, value)) Touch(); }
        }

        private string _inspectorName = string.Empty;
        public string InspectorName
        {
            get => _inspectorName;
            set { if (SetProperty(ref _inspectorName, value)) Touch(); }
        }

        private DateTime _plannedDate = DateTime.UtcNow;
        public DateTime PlannedDate
        {
            get => _plannedDate;
            set { if (SetProperty(ref _plannedDate, value)) Touch(); }
        }

        private DateTime? _completedDate;
        public DateTime? CompletedDate
        {
            get => _completedDate;
            set { if (SetProperty(ref _completedDate, value)) Touch(); }
        }

        private double? _latitude;
        public double? Latitude
        {
            get => _latitude;
            set { if (SetProperty(ref _latitude, value)) Touch(); }
        }

        private double? _longitude;
        public double? Longitude
        {
            get => _longitude;
            set { if (SetProperty(ref _longitude, value)) Touch(); }
        }

        private Guid _scheduledInspectionId;
        public Guid ScheduledInspectionId
        {
            get => _scheduledInspectionId;
            set { if (SetProperty(ref _scheduledInspectionId, value)) Touch(); }
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set { if (SetProperty(ref _notes, value)) Touch(); }
        }

        private int _violationCount;
        public int ViolationCount
        {
            get => _violationCount;
            set { if (SetProperty(ref _violationCount, value)) Touch(); }
        }

        private string _location = string.Empty;
        public string Location
        {
            get => _location;
            set { if (SetProperty(ref _location, value)) Touch(); }
        }

        // 🔹 Helper to mark dirty + update timestamp
        private void Touch()
        {
            LastModifiedUtc = DateTime.UtcNow;
            IsDirty = true;
        }

        // 🔹 INotifyPropertyChanged Implementation
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

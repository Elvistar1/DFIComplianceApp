using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DFIComplianceApp.Models
{
    public class User : ISyncEntity, INotifyPropertyChanged
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔹 Firebase UID (maps to Firebase Auth user)
        public string DFIUserId { get; set; } = string.Empty;

        // 🔹 Core Business Data
        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        private string _username;
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }


        private string _role;
        public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }
        public bool ForcePasswordChange { get; set; } = false;
        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                {
                    StatusText = value ? "Status: Active" : "Status: Inactive";
                    ButtonText = value ? "Deactivate" : "Activate";
                    StatusColorHex = value ? "#4CAF50" : "#F44336";
                }
            }
        }
        [Ignore] // do not persist in SQLite if you only need it for session
        public string IdToken { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ContactAddress { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔹 Sync Metadata
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public bool IsDirty { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        // 🔹 UI Helper Properties
        private string _statusText = "Status: Active";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _buttonText = "Deactivate";
        public string ButtonText
        {
            get => _buttonText;
            set => SetProperty(ref _buttonText, value);
        }

        private string _statusColorHex = "#4CAF50";
        public string StatusColorHex
        {
            get => _statusColorHex;
            set => SetProperty(ref _statusColorHex, value);
        }

        private bool _isSelected;
        [Ignore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        [Ignore]
        public bool CanEdit { get; set; } = false;

        [Ignore]
        public string DisplayNameWithId => $"{FullName} ({DFIUserId})";

        // 🔹 INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            IsDirty = true;
            LastModifiedUtc = DateTime.UtcNow;
            return true;
        }
    }
}

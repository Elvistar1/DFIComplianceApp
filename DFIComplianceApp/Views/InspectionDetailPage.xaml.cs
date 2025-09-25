using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DFIComplianceApp.Views
{
    public partial class InspectionDetailPage : ContentPage
    {
        public InspectionViewModel ViewModel { get; }

        public InspectionDetailPage(Guid inspectionId)
        {
            InitializeComponent();
            ViewModel = new InspectionViewModel();
            BindingContext = ViewModel;

            _ = ViewModel.LoadInspectionAsync(inspectionId, Navigation);
        }
    }

    public class InspectionViewModel : BindableObject
    {
        private List<InspectionAnswer> _answers;
        private INavigation _navigation;
        private bool _isLoading;

        public string CompanyName { get; private set; }
        public DateTime PlannedDate { get; private set; }
        public DateTime? CompletedDate { get; private set; }
        public string InspectorName { get; private set; }

        public List<InspectionAnswer> Answers
        {
            get => _answers;
            set
            {
                if (_answers != value)
                {
                    _answers = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        // ✅ Command for image tap
        public ICommand ImageTappedCommand { get; }

        public InspectionViewModel()
        {
            ImageTappedCommand = new Command<InspectionPhoto>(OnImageTapped);
        }

        public async Task LoadInspectionAsync(Guid inspectionId, INavigation navigation)
        {
            _navigation = navigation;
            IsLoading = true;

            try
            {
                var inspection = await App.Firebase.GetInspectionByIdAsync(inspectionId);
                if (inspection == null)
                {
                    await _navigation.PopAsync();
                    return;
                }

                CompanyName = inspection.CompanyName;
                PlannedDate = inspection.PlannedDate;
                CompletedDate = inspection.CompletedDate;
                InspectorName = inspection.InspectorName;

                OnPropertyChanged(nameof(CompanyName));
                OnPropertyChanged(nameof(PlannedDate));
                OnPropertyChanged(nameof(CompletedDate));
                OnPropertyChanged(nameof(InspectorName));

                await LoadAnswersAsync(inspectionId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAnswersAsync(Guid inspectionId)
        {
            var answers = await App.Firebase.GetAnswersForInspectionAsync(inspectionId);

            foreach (var answer in answers)
            {
                var photos = await App.Firebase.GetPhotosForAnswerAsync(answer.Id);

                // Debug log
                System.Diagnostics.Debug.WriteLine($"Answer: {answer.QuestionText} → {photos.Count} photos");
                foreach (var p in photos)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"  Photo: LocalPath={p.LocalPath}, Url={p.DownloadUrl}, Base64={(string.IsNullOrEmpty(p.PhotoBase64) ? "empty" : "set")}"
                    );
                }

                // ✅ Only keep PhotosObjects, no need for string list anymore
                answer.PhotosObjects = photos;
            }

            Answers = answers;
        }

        private async void OnImageTapped(InspectionPhoto photo)
        {
            if (photo != null && _navigation != null)
            {
                await _navigation.PushModalAsync(new PhotoPreviewPage(photo));
            }
        }
    }
}

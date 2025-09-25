using CommunityToolkit.Mvvm.Messaging;
using DFIComplianceApp.Messages;
using DFIComplianceApp.Models;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using DFIComplianceApp.Services;




namespace DFIComplianceApp.Views
{
    public partial class ConductInspectionPage : ContentPage
    {
        private readonly string _currentInspector;
        private readonly List<ChecklistItemVM> _checklist = new();
        private readonly ScheduledInspection _currentScheduledInspection;
        private bool _alreadyLoaded = false;


        // Static question bank by nature of work
        private static readonly Dictionary<string, string[]> _questions = new()
        {
            ["Oil & Gas Stations"] = new[]
            {
        "Is the premises registered and the certificate displayed?",
        "Are fire extinguishers adequate, serviced, and accessible?",
        "Are all fuel dispensing equipment and tanks maintained and inspected regularly?",
        "Are inflammable substances securely stored away from ignition sources?",
        "Is the premises clean and free from flammable waste?",
        "Are safe escape routes and fire exits marked and unobstructed?",
        "Is there adequate ventilation around storage and dispensing areas?",
        "Are emergency shut‑off devices clearly marked and functional?",
        "Is protective clothing provided for attendants?",
        "Are suitable arrangements made for confined space entry (tanks, pits)?",
        "Is adequate lighting provided for night operations?",
        "Are warning signs displayed (e.g. no smoking)?",
        "Is a register of lifting tackle maintained (for overhead tanks/lifting gear)?",
        "Is there adequate and safe drinking water provided for staff?",
        "Is a first‑aid box available and up to standard?"
    },
            ["Food Processing Companies"] = new[]
            {
        "Is the premises clean, free from refuse and regularly sanitized?",
        "Are production areas free from overcrowding and well‑ventilated?",
        "Are workers provided with appropriate protective clothing (aprons, gloves, head covers)?",
        "Is equipment clean and regularly maintained?",
        "Are harmful fumes, dust or vapours effectively removed?",
        "Is adequate washing and sanitary facilities provided for workers?",
        "Is food storage protected against contamination?",
        "Are food processing rooms properly ventilated and drained?",
        "Is eating or drinking prohibited in production areas?",
        "Are emergency exits functional and fire precautions enforced?",
        "Is adequate lighting maintained in food preparation and storage areas?",
        "Are cleaning schedules and records maintained?",
        "Is a first‑aid box available?",
        "Are accident records and occupational disease notifications up to date?"
    },
            ["Wood Processing Companies"] = new[]
            {
        "Is the premises registered and general register maintained?",
        "Are all moving parts of saws, planers, and other machinery guarded?",
        "Is dust extraction provided and effective at source?",
        "Are emergency stop devices fitted on all machines?",
        "Is there adequate space between machines for safe operation?",
        "Are fire extinguishers available, accessible, and serviced?",
        "Are protective equipment (ear defenders, goggles, gloves) provided?",
        "Is adequate ventilation and lighting maintained in work areas?",
        "Are wood dust and waste properly disposed of to prevent fire hazards?",
        "Are first‑aid boxes available?",
        "Are lifting equipment and tackle tested, marked, and logged?",
        "Are safe walkways, floors, and stairs maintained?",
        "Are dangerous areas securely fenced or clearly marked?",
        "Is there a register for accidents and dangerous occurrences?"
    },
            ["Warehouses"] = new[]
            {
        "Is the building safe and structurally sound?",
        "Are fire extinguishers strategically placed and maintained?",
        "Are racks and shelves secure and not overloaded?",
        "Are gangways clear of obstruction?",
        "Are safe access and escape routes maintained?",
        "Is mechanical lifting equipment tested and safe?",
        "Are warning notices displayed for hazardous areas?",
        "Is suitable lighting provided in all areas?",
        "Is there a first‑aid box available?",
        "Is drinking water and sanitary accommodation adequate?",
        "Are protective clothing and lifting aids provided where needed?",
        "Is fire‑fighting training conducted for staff?",
        "Is ventilation adequate to prevent buildup of harmful vapours?"
    },
            ["Sachet Water Production"] = new[]
            {
        "Is the premises clean and hygienic?",
        "Are machines used for production regularly cleaned and maintained?",
        "Is potable water used and periodically tested?",
        "Are protective clothing, gloves, and head covers provided for workers?",
        "Are sanitary facilities clean and segregated by sex?",
        "Is food or drink consumption prohibited in production areas?",
        "Is adequate lighting and ventilation available in the plant?",
        "Is a first‑aid box maintained?",
        "Are emergency exits functional and accessible?",
        "Are accidents and injuries properly recorded and reported?",
        "Are cleaning and disinfection schedules maintained?",
        "Are proper drainage and waste disposal systems in place?",
        "Is registration certificate displayed?",
        "Are packaging materials stored hygienically?"
    },
            ["Offices"] = new[]
            {
        "Is the premises registered?",
        "Is the office clean, ventilated and well lit?",
        "Are fire extinguishers available and easily accessible?",
        "Is drinking water provided and labeled?",
        "Are suitable washing and sanitary conveniences available?",
        "Are first‑aid facilities provided?",
        "Are escape routes and emergency exits available and unobstructed?",
        "Is adequate working space (minimum 40 sq ft per person) provided?",
        "Are floors, stairways, and corridors in good repair and free from obstruction?",
        "Are any records of accidents or dangerous occurrences maintained?",
        "Are dust and fumes properly controlled (if applicable)?"
    },
            ["Shops"] = new[]
            {
        "Is the shop registered and certificate displayed?",
        "Are fire safety arrangements in place (extinguishers, escape routes)?",
        "Is lighting and ventilation adequate for staff and customers?",
        "Are sanitary conveniences clean and accessible?",
        "Is potable drinking water available?",
        "Is a first‑aid box present and in good condition?",
        "Are accident records maintained?",
        "Are walkways and exits unobstructed?",
        "Is suitable seating provided for workers (if applicable)?",
        "Are floors sound, clean, and free from hazards?",
        "Is protective equipment provided where necessary?"
    },
            ["Manufacturing Companies"] = new[]
            {
        "Is the premises registered with an updated certificate?",
        "Are all machinery and moving parts safely guarded?",
        "Is lifting equipment tested, certified and safe?",
        "Are fire safety and escape provisions adequate?",
        "Are floors, stairs, and passages in sound condition and unobstructed?",
        "Are proper records of accidents, diseases, and dangerous occurrences kept?",
        "Are suitable sanitary, washing, and drinking water facilities available?",
        "Is dust, fumes, and noise effectively controlled?",
        "Are protective clothing and safety equipment provided?",
        "Are emergency stops on machines functional?",
        "Are workrooms adequately ventilated and lit?",
        "Are emergency drills conducted?",
        "Is medical supervision provided for hazardous processes?",
        "Are workers trained in safe operation of equipment?"
    }
        };

        public ICommand ImageTappedCommand => new Command<InspectionPhoto>(async (photo) =>
        {
            if (photo != null)
                await Navigation.PushModalAsync(new PhotoPreviewPage(photo));
        });


        public ConductInspectionPage(string inspectorUsername, ScheduledInspection scheduledInspection)
        {
            InitializeComponent();
            _currentInspector = inspectorUsername;
            _currentScheduledInspection = scheduledInspection;
            CategoryPicker.ItemsSource = _questions.Keys.ToList();
            BindingContext = this;
        }





        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!_alreadyLoaded)
            {
                _alreadyLoaded = true;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadInspectionDetailsAsync();
                });
            }
        }



        private async Task LoadInspectionDetailsAsync()
        {
            if (_currentScheduledInspection == null)
            {
                await DisplayAlert("Error", "No scheduled inspection found.", "OK");
                await Navigation.PopAsync();
                return;
            }

            if (_currentScheduledInspection.ScheduledDate.Date > DateTime.Today)
            {
                await DisplayAlert("Not Due",
                    $"Your assigned inspection for {_currentScheduledInspection.CompanyName} is scheduled for {_currentScheduledInspection.ScheduledDate:dd MMM yyyy}. You cannot conduct it before the scheduled date.",
                    "OK");
                await Navigation.PopAsync();
                return;
            }

            // Populate labels
            CompanyNameLabel.Text = _currentScheduledInspection.CompanyName;
            ScheduledDateLabel.Text = _currentScheduledInspection.ScheduledDate.ToString("dd MMM yyyy");
            FileNumberLabel.Text = _currentScheduledInspection.FileNumber ?? "(Not set)";
            LocationLabel.Text = _currentScheduledInspection.Location ?? "(Not set)";
            ContactLabel.Text = _currentScheduledInspection.Contact ?? "(Not set)";
            OccupierLabel.Text = _currentScheduledInspection.Occupier ?? "(Not set)";

            // Get NatureOfWork
            string? natureOfWork = await GetNatureOfWorkForCompany(_currentScheduledInspection.CompanyName);

            string selectedCategory = string.Empty;

            if (!string.IsNullOrWhiteSpace(natureOfWork))
            {
                // Try to match category ignoring case and trimming spaces
                selectedCategory = _questions.Keys
                    .FirstOrDefault(k => k.Trim().Equals(natureOfWork.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(selectedCategory))
            {
                CategoryPicker.SelectedItem = selectedCategory;
                LoadChecklistForCategory(selectedCategory);
                System.Diagnostics.Debug.WriteLine($"Loaded {_checklist.Count} questions for category: {selectedCategory}");
            }
            else
            {
                // Fallback: select first category if none matched
                selectedCategory = _questions.Keys.First();
                CategoryPicker.SelectedItem = selectedCategory;
                LoadChecklistForCategory(selectedCategory);
                System.Diagnostics.Debug.WriteLine($"No match from DB. Loaded default category: {selectedCategory} with {_checklist.Count} questions");
            }

            // Disable editing where necessary
            CategoryPicker.IsEnabled = false;
            CompanyPicker.IsEnabled = false;
            InspectionDatePicker.Date = _currentScheduledInspection.ScheduledDate;
            InspectionDatePicker.IsEnabled = false;
        }



        private async Task<string?> GetNatureOfWorkForCompany(string companyName)
        {
            var companies = await App.Firebase.GetCompaniesAsync();
            return companies.FirstOrDefault(c => c.Name == companyName)?.NatureOfWork;
        }

        private void LoadChecklistForCategory(string category)
        {
            _checklist.Clear();
            if (_questions.TryGetValue(category, out var questions))
            {
                for (int i = 0; i < questions.Length; i++)
                {
                    _checklist.Add(new ChecklistItemVM
                    {
                        QuestionNumber = i + 1,
                        Question = questions[i],
                        Answer = false,
                        Notes = string.Empty
                    });
                }
            }
            ChecklistCollectionView.ItemsSource = null;
            ChecklistCollectionView.ItemsSource = _checklist;
        }

        private async void OnCategoryChanged(object sender, EventArgs e)
        {
            if (CategoryPicker.SelectedItem is not string category) return;
            LoadChecklistForCategory(category);
        }

        private async void OnAttachPhotoClicked(object sender, EventArgs e)
        {
            await Task.Yield(); // 🔑 prevents COMException

            if (sender is not Button btn || btn.CommandParameter is not ChecklistItemVM item) return;

            if (!item.Answer)
            {
                await DisplayAlert("Not Allowed", "You can only add a photo if the item is marked as compliant.", "OK");
                return;
            }

            try
            {
                var result = await MediaPicker.CapturePhotoAsync();
                if (result == null) return;

                string ext = Path.GetExtension(result.FileName);
                string path = Path.Combine(FileSystem.AppDataDirectory, $"{Guid.NewGuid()}{ext}");

                await using var src = await result.OpenReadAsync();
                await using var dst = File.OpenWrite(path);
                await src.CopyToAsync(dst);

                item.PhotoPaths.Add(path);

                ChecklistCollectionView.ItemsSource = null;
                ChecklistCollectionView.ItemsSource = _checklist;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Photo Error", ex.Message, "OK");
            }
        }


        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                // 🔹 Show loading overlay
                LoadingOverlay.IsVisible = true;
                SaveButton.IsEnabled = false;

                if (_currentScheduledInspection == null)
                {
                    await DisplayAlert("Validation", "No scheduled inspection found.", "OK");
                    return;
                }

                // Check for compliant items without photos
                var missingPhotoItems = _checklist
                    .Where(item => item.Answer && (item.PhotoPaths == null || item.PhotoPaths.Count == 0))
                    .ToList();

                if (missingPhotoItems.Any())
                {
                    string questions = string.Join("\n", missingPhotoItems.Select(i => $"{i.QuestionNumber}. {i.Question}"));
                    await DisplayAlert("Photo Required",
                        $"The following compliant items are missing photos:\n\n{questions}\n\nPlease add a photo for each before saving.",
                        "OK");
                    LoadingOverlay.IsVisible = false;
                    SaveButton.IsEnabled = true;
                    return;
                }
                        
                int violationCount = _checklist.Count(item => !item.Answer);

                // Create the main inspection object
                var inspection = new Inspection
                {
                    Id = Guid.NewGuid(),
                    CompanyId = _currentScheduledInspection.CompanyId,
                    InspectorName = _currentInspector,
                    ScheduledInspectionId = _currentScheduledInspection.Id,
                    PlannedDate = _currentScheduledInspection.ScheduledDate,
                    CompletedDate = DateTime.Now,
                    CompanyName = _currentScheduledInspection.CompanyName,
                    Location = _currentScheduledInspection.Location,
                    ViolationCount = violationCount
                };

                await App.Firebase.SaveInspectionAsync(inspection);

                // Save each checklist answer and its photos
                foreach (var item in _checklist)
                {
                    var answer = new InspectionAnswer
                    {
                        Id = Guid.NewGuid(),
                        InspectionId = inspection.Id,
                        QuestionText = item.Question,
                        IsCompliant = item.Answer,
                        Notes = item.Notes
                    };

                    await App.Firebase.SaveInspectionAnswerAsync(answer);

                    if (item.PhotoPaths != null && item.PhotoPaths.Count > 0)
                    {
                        foreach (var path in item.PhotoPaths)
                        {
                            try
                            {
                                byte[] photoBytes = File.ReadAllBytes(path);
                                string base64 = Convert.ToBase64String(photoBytes);
                                string fileName = Path.GetFileName(path);

                                // Save as Base64 string in Firebase (update your SaveInspectionPhotoAsync to accept base64)
                                await App.Firebase.SaveInspectionPhotoAsync(
                                    answer.Id.ToString(),
                                    fileName,
                                    null, // pass null for photoBytes if not used
                                    path,
                                    base64 // add this parameter to your method
                                );
                            }
                            catch (Exception ex)
                            {
                                await DisplayAlert("Photo Error", $"Failed to save photo {path}: {ex.Message}", "OK");
                            }
                        }
                    }
                }

                // Now queue for AI report using the real inspection.Id
                await QueueForAiReportAsync(inspection.Id, _currentScheduledInspection.CompanyName,
                    CategoryPicker.SelectedItem?.ToString() ?? "", _checklist);

                // Refresh UI and notify
                MessagingCenter.Send(this, "RefreshUpcomingInspections");
                WeakReferenceMessenger.Default.Send(new InspectionCompletedMessage());

                // Notify InspectorDashboardPage to refresh immediately
                MessagingCenter.Send(this, "RefreshInspectorDashboard");

                // Email notification (unchanged)
                try
                {
                    string companyEmail = null;
                    string occupier = _currentScheduledInspection.Occupier ?? "Sir/Madam";
                    string companyName = _currentScheduledInspection.CompanyName;

                    var companies = await App.Firebase.GetCompaniesAsync();
                    var company = companies.FirstOrDefault(c => c.Name == companyName);
                    if (company != null)
                    {
                        companyEmail = company.Email;
                        occupier = company.Occupier ?? occupier;
                    }

                    if (!string.IsNullOrWhiteSpace(companyEmail))
                    {
                        string subject = $"Inspection Completed: {companyName}";
                        string body = $"Dear {occupier},\n\n" +
                                      $"An inspection was conducted at your premises on {inspection.CompletedDate:dd MMM yyyy}.\n" +
                                      $"Number of violations: {inspection.ViolationCount}\n\n" +
                                      "You will receive an advisory report within 24 hours.\n\n" +
                                      "Thank you for your cooperation.\n\n" +
                                      "Regards,\nDepartment of Factories Inspectorate";

                        var emailService = App.Services.GetRequiredService<IEmailService>();
                        await emailService.SendAsync(companyEmail, subject, body, false);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Email notification failed: {ex.Message}");
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Saved", "Inspection saved successfully.", "OK");
                });


                // Go back to previous page (UpcomingInspectionsPage)
                if (App.CurrentUser != null)
                {
                    await Navigation.PopAsync();
                }
                else
                {
                    await Navigation.PopToRootAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Save failed: {ex.Message}", "OK");
            }
            finally
            {
                // 🔹 Hide loading overlay
                LoadingOverlay.IsVisible = false;
                SaveButton.IsEnabled = true;
            }
        }






        private async Task QueueForAiReportAsync(Guid inspectionId, string premises, string natureOfWork, List<ChecklistItemVM> items)
        {
            if (inspectionId == Guid.Empty)
                throw new InvalidOperationException("Cannot queue AI report: inspectionId is empty.");

            var obj = new
            {
                premises,
                natureOfWork,
                answers = items.Select(i => new
                {
                    question = i.Question,
                    compliant = i.Answer,
                    notes = i.Notes,
                    photoPaths = i.PhotoPaths
                }).ToList()
            };

            string json = JsonSerializer.Serialize(obj);

            // ✅ Save only PendingAiReport
            await App.Firebase.SavePendingReportAsync(new PendingAiReport
            {
                Id = Guid.NewGuid().ToString(),   // if your PendingAiReport.Id is string
                InspectionId = inspectionId,
                Json = json,
                Status = "Queued",
                CreatedAt = DateTime.UtcNow,
                InspectorUsername = _currentInspector
            });

            // ❌ REMOVE this block (no direct AIReport creation here!)
            // var aiReport = await App.Firebase.GetAIReportByInspectionIdAsync(inspectionId);
            // if (aiReport == null) { ... }
        }




        private sealed class ChecklistItemVM : INotifyPropertyChanged
        {
            public int QuestionNumber { get; set; }
            public string Question { get; set; } = string.Empty;
            bool _answer;
            public bool Answer
            {
                get => _answer;
                set { if (_answer != value) { _answer = value; OnPropertyChanged(); } }
            }
            public string Notes { get; set; } = string.Empty;
            public List<string> PhotoPaths { get; } = new();
            public event PropertyChangedEventHandler? PropertyChanged;
            void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


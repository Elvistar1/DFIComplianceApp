using DFIComplianceApp.Helpers;
using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;


namespace DFIComplianceApp.Views
{
    public partial class UpcomingInspectionsPage : ContentPage
    {
        private readonly User _currentUser;
        private bool _isBusy = false;

        public ObservableCollection<ScheduledInspection> UpcomingInspections { get; } = new();

        public UpcomingInspectionsPage(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }
        public ICommand InspectionTappedCommand => new Command<ScheduledInspection>(async (selected) =>
        {
            if (selected == null) return;

            if (selected.ScheduledDate.Date != DateTime.Today)
            {
                await DisplayAlert("Not Allowed", "You can only conduct this inspection on the scheduled date.", "OK");
                return;
            }

            await Navigation.PushAsync(new ConductInspectionPage(_currentUser.Username, selected));
        });

        private async Task LoadAsync()
        {
            if (_isBusy) return;

            try
            {
                _isBusy = true;

                var allScheduled = await App.Firebase.GetAllScheduledInspectionsAsync() ?? new List<ScheduledInspection>();
                var completedInspections = await App.Firebase.GetInspectionsAsync() ?? new List<Inspection>();

                // Debug output
                System.Diagnostics.Debug.WriteLine("---- Scheduled ----");
                foreach (var s in allScheduled)
                    System.Diagnostics.Debug.WriteLine($"Scheduled: {s.Id} {s.CompanyName} {s.ScheduledDate}");

                System.Diagnostics.Debug.WriteLine("---- Completed ----");
                foreach (var c in completedInspections)
                    System.Diagnostics.Debug.WriteLine($"Completed: {c.Id} ScheduledInspectionId={c.ScheduledInspectionId}");

                var completedScheduledIds = completedInspections
                    .Where(i => i.ScheduledInspectionId != Guid.Empty)
                    .Select(i => i.ScheduledInspectionId)
                    .ToHashSet();

                UpcomingInspections.Clear();

                foreach (var x in allScheduled.OrderBy(s => s.ScheduledDate))
                {
                    try
                    {
                        var inspectorIds = JsonSerializer.Deserialize<List<string>>(x.InspectorIdsJson ?? "[]") ?? new();
                        var inspectorUsernames = JsonSerializer.Deserialize<List<string>>(x.InspectorUsernamesJson ?? "[]") ?? new();

                        bool isAssigned = inspectorIds.Contains(_currentUser.Id.ToString()) ||
                                          inspectorUsernames.Contains(_currentUser.Username);

                        bool isNotCompleted = !completedScheduledIds.Contains(x.Id);

                        if (isAssigned && isNotCompleted && x.ScheduledDate >= DateTime.Today)
                        {
                            UpcomingInspections.Add(x);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Deserialization failed for ScheduledInspection Id {x.Id}: {ex.Message}");
                    }
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async void OnInspectionTapped(object sender, SelectionChangedEventArgs e)
        {
#if WINDOWS || ANDROID || IOS || MACCATALYST
            System.Diagnostics.Debug.WriteLine("OnInspectionTapped fired");
            var selection = e.CurrentSelection?.FirstOrDefault();
            if (selection is ScheduledInspection selected)
            {
                System.Diagnostics.Debug.WriteLine($"Selected: {selected.CompanyName}, Date: {selected.ScheduledDate}");
                if (sender is CollectionView cv)
                    cv.SelectedItem = null;

                if (selected.ScheduledDate.Date != DateTime.Today)
                {
                    System.Diagnostics.Debug.WriteLine("Scheduled date is not today, navigation blocked.");
                    await DisplayAlert("Not Allowed", "You can only conduct this inspection on the scheduled date.", "OK");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"Navigating to ConductInspectionPage for: {selected.CompanyName}, {selected.ScheduledDate}");

                try
                {
                    await Navigation.PushAsync(new ConductInspectionPage(_currentUser.Username, selected));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation failed: {ex}");
                    await DisplayAlert("Error", ex.Message, "OK");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Selection is not a ScheduledInspection.");
            }
#endif
        }
        private async void OnExportClicked(object sender, EventArgs e)
        {
            if (_isBusy || UpcomingInspections.Count == 0)
            {
                await DisplayAlert("Export", "No upcoming inspections to export.", "OK");
                return;
            }

            try
            {
                var displayRows = UpcomingInspections.Select(i => new ScheduledInspectionDisplay
                {
                    CompanyName = i.CompanyName,
                    FileNumber = i.FileNumber,
                    Location = i.Location,
                    Contact = i.Contact,
                    Occupier = i.Occupier,
                    InspectorUsernames = i.DisplayInspectorNames,
                    ScheduledDate = i.ScheduledDate,
                    Notes = i.Notes
                }).ToList();

                await ExportHelper.ExportScheduledInspectionsAsync(displayRows, "UpcomingInspections");
                await DisplayAlert("Export", "Export completed successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Failed", ex.Message, "OK");
            }
        }
    }
}

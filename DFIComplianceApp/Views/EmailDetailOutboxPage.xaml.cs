using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Views
{
    public partial class EmailDetailOutboxPage : ContentPage
    {
        private readonly IAppDatabase _db;
        private OutboxMessage _email;

        public EmailDetailOutboxPage(OutboxMessage email)
        {
            InitializeComponent();
            _db = App.Services.GetRequiredService<IAppDatabase>();
            _email = email;
            BindingContext = _email;
        }

        private async void OnApproveClicked(object sender, EventArgs e)
        {
            if (_email.MeetingDate.HasValue)
            {
                var appointment = new Appointment
                {
                    CompanyName = _email.CompanyName,
                    MeetingDate = _email.MeetingDate.Value,
                    Subject = _email.Subject,
                    Details = _email.Body,  // Changed from BodyPlainText to Body
                    Source = "OutboxEmail"
                };

                await _db.SaveAppointmentAsync(appointment);
                await DisplayAlert("Approved", "Appointment saved successfully.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Meeting date is missing.", "OK");
            }
        }

        private async void OnScheduleAppointmentClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ScheduleAppointmentFromEmailPage(_email.CompanyName, _email.Subject));
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            await _db.DeleteOutboxAsync(_email);
            await DisplayAlert("Deleted", "Email removed from queue.", "OK");
            await Navigation.PopAsync();
        }
    }
}

using DFIComplianceApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace DFIComplianceApp.Views;

public partial class ScheduleAppointmentFromEmailPage : ContentPage
{
    private readonly IEmailService _emailService;

    // Optional parameters for company and purpose
    public ScheduleAppointmentFromEmailPage(string company = "", string purpose = "")
    {
        InitializeComponent();

        // ✅ Resolve from MAUI DI container
        _emailService = App.Services.GetService<IEmailService>()
                        ?? throw new InvalidOperationException("IEmailService not registered in DI");

        // Pre-fill entries if values provided
        CompanyEntry.Text = company;
        PurposeEntry.Text = purpose;
    }

    private async void OnScheduleClicked(object sender, EventArgs e)
    {
        string company = CompanyEntry.Text?.Trim();
        string purpose = PurposeEntry.Text?.Trim();
        DateTime date = AppointmentDatePicker.Date;
        TimeSpan time = AppointmentTimePicker.Time;

        // Validate inputs
        if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(purpose))
        {
            await DisplayAlert("Error", "Please fill in all fields.", "OK");
            return;
        }

        DateTime appointmentDateTime = date.Date + time;

        if (appointmentDateTime < DateTime.Now)
        {
            await DisplayAlert("Error", "Appointment date and time must be in the future.", "OK");
            return;
        }

        string subject = $"Appointment Scheduled with {company}";
        string body = $"An appointment has been scheduled for {company}.\n\n" +
                      $"Purpose: {purpose}\n" +
                      $"Date & Time: {appointmentDateTime:dd MMM yyyy hh:mm tt}";

        try
        {
            await _emailService.SendAsync("recipient@example.com", subject, body, false);

            await DisplayAlert("Success",
                $"Appointment scheduled for {company} on {appointmentDateTime:dd MMM yyyy hh:mm tt}\nEmail sent.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Appointment saved but email failed: {ex.Message}", "OK");
        }

        await Navigation.PopAsync();
    }
}

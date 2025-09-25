using DFIComplianceApp.Services;
using DFIComplianceApp.ViewModels;

namespace DFIComplianceApp.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel(
            this.Navigation,
            App.Services.GetRequiredService<FirebaseAuthService>(),   // handles login/logout/reset
            App.Services.GetRequiredService<IAppDatabase>());         // local DB if you still need to store user/session metadata
    }
}

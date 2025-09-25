using DFIComplianceApp.ViewModels;
using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Views
{
    [QueryProperty(nameof(Token), "token")]
    public partial class ResetPasswordPage : ContentPage
    {
        public ResetPasswordPage(ResetPasswordViewModel vm)
        {
            InitializeComponent();
            vm.InitNavigation(Navigation);
            BindingContext = vm;
        }

        public string Token
        {
            set
            {
                if (BindingContext is ResetPasswordViewModel vm)
                    vm.Token = value;
            }
            get => string.Empty;
        }
    }
}

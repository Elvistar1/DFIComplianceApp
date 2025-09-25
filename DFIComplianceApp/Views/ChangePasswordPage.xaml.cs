using DFIComplianceApp.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace DFIComplianceApp.Views
{
    public partial class ChangePasswordPage : ContentPage
    {
        public ChangePasswordPage()
        {
            InitializeComponent();

            var vm = App.Services.GetRequiredService<ChangePasswordViewModel>();
            vm.InitNavigation(Navigation); // inject navigation
            BindingContext = vm;
        }
    }
}

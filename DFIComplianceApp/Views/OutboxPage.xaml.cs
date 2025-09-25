using Microsoft.Maui.Controls;
using DFIComplianceApp.ViewModels;

namespace DFIComplianceApp.Views          // ← must match the x:Class in XAML
{
    public partial class OutboxPage : ContentPage
    {
        private readonly OutboxViewModel _vm;

        public OutboxPage(OutboxViewModel vm)      // DI supplies the VM
        {
            InitializeComponent();                 // now resolves
            BindingContext = _vm = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadAsync();                 // refresh list each visit
        }
    }
}

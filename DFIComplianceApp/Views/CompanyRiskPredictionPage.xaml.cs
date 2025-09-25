using DFIComplianceApp.Services;
using DFIComplianceApp.ViewModels;

namespace DFIComplianceApp.Views;

public partial class CompanyRiskPredictionPage : ContentPage
{
    public CompanyRiskPredictionPage()
    {
        InitializeComponent();
        BindingContext = new CompanyRiskPredictionViewModel();
    }
}
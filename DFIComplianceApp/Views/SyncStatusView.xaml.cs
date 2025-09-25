using Microsoft.Maui.Controls;
using DFIComplianceApp.Services;    

namespace DFIComplianceApp.Views;

public partial class SyncStatusView : ContentView
{
    public SyncStatusView()
    {

        InitializeComponent();

        BindingContext = MauiProgram.Services.GetService<SyncService>();
    }
}
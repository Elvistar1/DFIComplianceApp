using Android.App;
using Android.Runtime;
using Microsoft.Maui;

namespace DFIComplianceApp
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            // No additional setup needed here for CommunityToolkit.Maui
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}

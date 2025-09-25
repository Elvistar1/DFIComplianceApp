using Foundation;
using UIKit; // Required for UIApplication
using System;
using Microsoft.Maui;

namespace DFIComplianceApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        // 🔑 Handle custom URL scheme (e.g. dfireset://reset?token=abc123)
        public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            // This passes the deep link to MAUI's app system
            App.Current?.SendOnAppLinkRequestReceived(new Uri(url.AbsoluteString));
            return true;
        }
    }
}

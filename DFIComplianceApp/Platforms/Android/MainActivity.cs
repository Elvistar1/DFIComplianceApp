using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Net;          // Android.Net.Uri
using System;               // System.Uri

namespace DFIComplianceApp
{
    [Activity(
        Label = "DFIComplianceApp",
        Theme = "@style/MainTheme", // ✅ Apply MaterialComponents theme
        MainLauncher = true,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize)]

    // ─── Intent filter for dfireset://reset?token=XYZ ───
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[]
        {
            Intent.CategoryDefault,
            Intent.CategoryBrowsable
        },
        DataScheme = "dfireset",
        DataHost = "reset")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Handle a link that launched the app cold-start
            if (Intent?.Data != null)
                ProcessDeepLink(Intent.Data);
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            // Handle link while app is already running
            if (intent?.Data != null)
                ProcessDeepLink(intent.Data);
        }

        // ★ Use Android.Net.Uri to avoid ambiguity
        private static void ProcessDeepLink(Android.Net.Uri androidUri)
        {
            // Convert to System.Uri and forward to MAUI
            if (App.Current != null && androidUri != null)
            {
                var uri = new System.Uri(androidUri.ToString());
                App.Current.SendOnAppLinkRequestReceived(uri);
            }
        }
    }
}

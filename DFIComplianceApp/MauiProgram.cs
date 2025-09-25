using CommunityToolkit.Maui;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using DFIComplianceApp.ViewModels;
using Microcharts.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using LiveChartsCore.SkiaSharpView.Maui;
using DFIComplianceApp.Views; // for MessagingPage

#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Maui.Platform;
using AppUI = Microsoft.Maui.Controls.Application;
using System.Web;
using Windows.ApplicationModel.Activation;
#endif

namespace DFIComplianceApp;

public static class MauiProgram
{
    // ── static DI container for Android/iOS App() constructor ──
    public static IServiceProvider Services { get; private set; }

#if WINDOWS
    private static bool handlingProtocol;
#endif

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // ── JSON configuration ──
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);
#if ANDROID
        builder.Configuration.AddJsonFile("appsettings.android.json", optional: true, reloadOnChange: false);
#elif WINDOWS
        builder.Configuration.AddJsonFile("appsettings.windows.json", optional: true);
#endif

        // ── Core MAUI setup ──
        builder
            .UseMauiApp<App>()
            .UseMicrocharts()
            .UseMauiCommunityToolkit()
            .UseLiveCharts()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("FontAwesomeSolid.otf", "FontAwesomeSolid");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

#if WINDOWS
        // ── Windows deep-link handling ──
        builder.ConfigureLifecycleEvents(ev =>
        {
            ev.AddWindows(win =>
            {
                win.OnLaunched((_, _) => HandleProtocol());
                win.OnActivated((_, _) => HandleProtocol());

                void HandleProtocol()
                {
                    if (handlingProtocol) return;
                    handlingProtocol = true;

                    try
                    {
                        AppActivationArguments actArgs;
                        try
                        {
                            actArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                        }
                        catch (COMException ex) when ((uint)ex.HResult == 0x8001010D)
                        {
                            return;
                        }

                        if (actArgs.Kind != ExtendedActivationKind.Protocol) return;

                        if (actArgs.Data is IProtocolActivatedEventArgs p &&
                            p.Uri.Scheme == "dfireset" &&
                            p.Uri.Host == "reset")
                        {
                            var token = HttpUtility.ParseQueryString(p.Uri.Query).Get("token");
                            if (string.IsNullOrWhiteSpace(token)) return;

                            var sp = MauiWinUIApplication.Current.Services;
                            var resetPage = sp.GetRequiredService<Views.ResetPasswordPage>();

                            if (resetPage.BindingContext is ResetPasswordViewModel vm)
                                vm.Token = token;

                            AppUI.Current.Dispatcher.Dispatch(() =>
                            {
                                AppUI.Current.MainPage = new NavigationPage(resetPage)
                                {
                                    BarBackgroundColor = Colors.White,
                                    BarTextColor = Colors.Black
                                };
                            });
                        }
                    }
                    finally
                    {
                        handlingProtocol = false;
                    }
                }
            });
        });
#endif

        // ── Register services ──
        var smsSettings = builder.Configuration.GetSection("Twilio").Get<SmsSettings>() ?? new();
        builder.Services.AddSingleton(smsSettings);
        builder.Services.AddSingleton<ISmsSender, TwilioSmsSender>();
        builder.Services.AddSingleton<FirebaseAuthService>();

        var emailSettings = builder.Configuration.GetSection("Smtp").Get<EmailSettings>() ?? new();
        builder.Services.AddSingleton(emailSettings);
        builder.Services.AddSingleton<EmailService>();
        builder.Services.AddSingleton<RenewalReminderService>();

        // --- Messaging services: online + offline + hybrid ---


        var appDb = new AppDatabase(DatabaseConstants.DbPath);
        builder.Services.AddSingleton<IAppDatabase>(appDb);
        builder.Services.AddSingleton<AppDatabase>(appDb);
     
       
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<EmailReceiverService>();
        builder.Services.AddSingleton<EmailBackgroundService>();
        builder.Services.AddSingleton<IEmailService, EmailService>();

        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Smtp"));

        // Expert system & advice services
        builder.Services.AddSingleton<OpenRouterAdviceService>();
        builder.Services.AddSingleton<IExpertSystemService, ExpertSystemService>();
        builder.Services.AddSingleton<IAdviceService>(sp => sp.GetRequiredService<OpenRouterAdviceService>());

        builder.Services.AddSingleton<IEmailService>(sp =>
        {
            var smtp = sp.GetRequiredService<EmailService>();
            var db = sp.GetRequiredService<IAppDatabase>();
            return new ReliableEmailService(smtp, db);
        });

        builder.Services.AddSingleton<OutboxSyncService>();
        builder.Services.AddSingleton<OutboxBackgroundFlusher>();
        builder.Services.AddSingleton<ConnectivityOutboxFlusher>();
        builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();


        builder.Services.AddSingleton<SyncService>(); // <-- Register SyncService here

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<ResetPasswordViewModel>();
        builder.Services.AddTransient<OutboxViewModel>();
      
        // Pages
        builder.Services.AddTransient<Views.LoginPage>();
        builder.Services.AddTransient<Views.ForgotPasswordPage>();
        builder.Services.AddTransient<Views.ChangePasswordPage>();
        builder.Services.AddTransient<Views.ResetPasswordPage>();
        builder.Services.AddTransient<Views.DirectorDashboardPage>();
        builder.Services.AddTransient<Views.InspectorDashboardPage>();
        builder.Services.AddTransient<Views.SecretaryDashboardPage>();
        builder.Services.AddTransient<Views.OutboxPage>();
        builder.Services.AddTransient<Views.CompanyRegistrationPage>();
        
        

        // Navigation page
        builder.Services.AddTransient<NavigationPage>(provider => new NavigationPage
        {
            BarBackgroundColor = Color.FromArgb("#1976D2"),
            BarTextColor = Colors.White
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // ✅ Store DI container globally for Android/iOS App()
        Services = app.Services;

        Console.WriteLine($"[DEBUG] SMTP Host: {emailSettings?.Host}");

        return app;
    }
}

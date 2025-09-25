using CommunityToolkit.Maui.Alerts;
using DFIComplianceApp.Models;
using DFIComplianceApp.Services;
using DFIComplianceApp.ViewModels;
using DFIComplianceApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DFIComplianceApp;

public partial class App : Application
{
    /* ─── GLOBAL SINGLETON DATA ─────────────────────────── */
    public static List<AuditLog> AuditLogs { get; } = new();
    public static List<ScheduledInspection> ScheduledInspections { get; } = new();
    public static User? CurrentUser { get; set; }
    public static FirebaseAuthService Firebase { get; private set; } = null!;
    public static string? FirebaseIdToken { get; set; }

    /* ─── LAZY DB ───────────────────────────────────────── */
    private static AppDatabase? _database;
    public static AppDatabase Database => _database ??= new AppDatabase(DatabaseConstants.DbPath);

    /* ─── DI CONTAINER ─────────────────────────────────── */
    private readonly IServiceProvider _services;
    private readonly EmailReceiverService _emailReceiver;

    public static IServiceProvider Services =>
        (Current as App)?._services ?? throw new InvalidOperationException("App.Current is not of type App or DI not initialized");

    // 🔹 Expose a task representing all startup work
    public static Task StartupTasks { get; private set; } = Task.CompletedTask;

    // ─── Parameterless constructor for Android/iOS
    public App() : this(MauiProgram.Services) { }

    // ─── Constructor with DI for Windows or explicit DI
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _services = serviceProvider;

        // 🔹 Guarantee Firebase + Database are resolved immediately
        Firebase = _services.GetRequiredService<FirebaseAuthService>();
        _database = _services.GetRequiredService<AppDatabase>();

        // Resolve EmailReceiverService from DI container
        _emailReceiver = _services.GetRequiredService<EmailReceiverService>();

        // Start checking emails asynchronously (fire & forget)
        _ = _emailReceiver.CheckNewEmailsAsync();

        // 🔹 Run startup tasks concurrently and expose as StartupTasks
        StartupTasks = Task.Run(async () =>
        {
            try
            {
                var syncService = _services.GetRequiredService<SyncService>();
                await Task.WhenAll(
                    CopyModelToAppDataDirectoryAsync(),
                    syncService.SyncUsersAsync()
                );

                // Ensure DB is initialized before sync
                await Database.EnsureInitializedAsync();
                await syncService.RunSyncNowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup tasks failed: {ex}");
            }
        });

        // Set MainPage to login
        var loginPage = _services.GetRequiredService<LoginPage>();
        MainPage = new NavigationPage(loginPage)
        {
            BarBackgroundColor = Colors.White,
            BarTextColor = Colors.Black
        };
    }

    // ─── Copy ML.NET model to AppData directory
    public static async Task CopyModelToAppDataDirectoryAsync()
    {
        var fileName = "company_risk_model.zip";
        var destinationPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        if (!File.Exists(destinationPath))
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            using var outStream = File.Create(destinationPath);
            await stream.CopyToAsync(outStream);
        }
    }

    // ─── Maintenance mode check
    public static async Task<bool> CheckMaintenanceModeAsync(string currentRole)
    {
        var settings = await Database.GetAppSettingsAsync();
        if (settings?.MaintenanceMode == true &&
            !string.Equals(currentRole, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            await Application.Current.MainPage.DisplayAlert(
                "Maintenance Mode",
                "🚧 The system is under maintenance. Please contact the administrator.",
                "OK");
            return true;
        }
        return false;
    }

    // ─── Handle dfireset://reset?token=XYZ deep-link
    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        System.Diagnostics.Debug.WriteLine($"APP-LINK RECEIVED: {uri}");
        base.OnAppLinkRequestReceived(uri);

        if (uri.Scheme == "dfireset" && uri.Host == "reset")
        {
            var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var token = qs.Get("token");

            if (!string.IsNullOrWhiteSpace(token))
            {
                var resetPage = _services.GetRequiredService<ResetPasswordPage>();

                if (resetPage.BindingContext is ViewModels.ResetPasswordViewModel vm)
                    vm.Token = token;
                else
                    System.Diagnostics.Debug.WriteLine("ResetPasswordPage binding context is not set or wrong type.");

                MainPage.Dispatcher.Dispatch(() =>
                {
                    MainPage = new NavigationPage(resetPage)
                    {
                        BarBackgroundColor = Colors.White,
                        BarTextColor = Colors.Black
                    };
                });
            }
        }
    }

    // On app startup
    public async Task InitializeAppAsync()
    {
        var firebaseAuthService = Services.GetRequiredService<FirebaseAuthService>();
        var localDb = Services.GetRequiredService<AppDatabase>();

        // Get all users from Firebase
        var remoteUsers = await firebaseAuthService.GetAllUsersAsync();
        if (remoteUsers.Count == 0)
        {
            // Seed users locally and push to Firebase
            var seededUsers = SeedUsers();
            foreach (var user in seededUsers)
            {
                var existingUser = await localDb.GetUserByUsernameAsync(user.Username);
                if (existingUser == null)
                {
                    await localDb.SaveUserAsync(user);
                    await firebaseAuthService.PushUserAsync(user);
                }
            }
        }
        else
        {
            // Populate local DB from Firebase
            foreach (var user in remoteUsers)
                await localDb.SaveUserAsync(user);
        }
    }

    // Add this method to your App class
    private static List<User> SeedUsers()
    {
        return new List<User>
        {
            new User { FullName = "Admin User", Username = "admin", Email = "admin@example.com", Role = "Administrator", IsActive = true },
            new User { FullName = "Director User", Username = "director", Email = "director@example.com", Role = "Director", IsActive = true },
            new User { FullName = "Inspector User", Username = "inspector", Email = "inspector@example.com", Role = "Inspector", IsActive = true },
            new User { FullName = "Secretary User", Username = "secretary", Email = "secretary@example.com", Role = "Secretary", IsActive = true }
        };
    }

    protected override async void OnStart()
    {
        var syncService = _services.GetRequiredService<SyncService>();
        var localDb = Services.GetRequiredService<AppDatabase>();

        await localDb.EnsureInitializedAsync(); // Ensure DB is ready

        var localUsers = await localDb.GetUsersAsync();
        if (localUsers == null || localUsers.Count == 0)
        {
            await syncService.SyncUsersAsync();
        }

        // ✅ NEW: Preload AI key from SecureStorage so AdviceService works immediately
        try
        {
            var key = await SecureStorage.GetAsync("OpenRouterKey");
            if (!string.IsNullOrWhiteSpace(key))
            {
                if (Services.GetService<IAdviceService>() is OpenRouterAdviceService routerSvc)
                {
                    await routerSvc.RefreshKeyAsync();
                    System.Diagnostics.Debug.WriteLine("✅ OpenRouter API key loaded at startup.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ No OpenRouter API key found in SecureStorage.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Failed to preload OpenRouter key: {ex}");
        }
    }
}

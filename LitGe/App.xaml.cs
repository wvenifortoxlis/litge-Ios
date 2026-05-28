using System.Timers;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using LitGe.Lib;
using LitGe.Lib.Services;
using LitGe.Pages;

namespace LitGe
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // FORCE CLEAR OLD CORRUPTED DATA
            if (!Preferences.Default.ContainsKey("DataClearedForV16"))
            {
                try
                {
                    // Clear Session JSON file
                    string sessionFile = Path.Combine(FileSystem.Current.AppDataDirectory, "__session.json");
                    if (File.Exists(sessionFile)) File.Delete(sessionFile);

                    // Clear Keys from SecureStorage
                    SecureStorage.Default.RemoveAll();

                    Preferences.Default.Set("DataClearedForV16", true);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"DEBUG_APP: Failed to clear old data {e}");
                }
            }

            MainPage = new AppShell();
            // ConnectivityChanged subscription is fine in constructor
            Connectivity.Current.ConnectivityChanged += Current_ConnectivityChanged;
        }

        protected override void OnStart()
        {
            base.OnStart();
            // Perform initial network check after the app has started and Shell is more likely to be ready
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                NetworkCheck();
            }
        }

        private void Current_ConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            NetworkCheck();
        }

        private async void NetworkCheck()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                if (Shell.Current != null)
                {
                    try {
                        await Shell.Current.GoToAsync(nameof(NoInternetRealm), true);
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"NetworkCheck navigation failed: {ex.Message}");
                    }
                }
            }
        }
        protected override void OnSleep()
        {
            base.OnSleep();
            MessagingCenter.Send(this, "AppSleep");
        }

        protected override void OnResume()
        {
            base.OnResume();
            MessagingCenter.Send(this, "AppResume");
        }
    }
}

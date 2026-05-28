using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Animations;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using LitGe.Lib;
using LitGe.Lib.Services;
using LitGe.Pages;
using ResourceManager = LitGe.Lib.ResourceManager.ResourceManager;

namespace LitGe
{
    public partial class MainPage : ContentPage
    {
        //private double sheetStartY = 0; // The starting Y position of the sheet before dragging
        //private double totalY = 0;      // Total movement of the drag

        [Obsolete]
        public MainPage(ResourceManager resourceManager)
        {
            InitializeComponent();

            DeviceDisplay.MainDisplayInfoChanged += async (sender, args) =>
            {
                DisplayOrientation orientation = args.DisplayInfo.Orientation;

                CancellationTokenSource cts = new();
                ToastDuration td = ToastDuration.Short;

                if (orientation == DisplayOrientation.Landscape)
                {
                    IToast toast = Toast.Make("Landscape", td, 16);

                    MessagingCenter.Send<MainPage, SCREEN_DISPLAY>(
                        this,
                        nameof(SCREEN_DISPLAY),
                        SCREEN_DISPLAY.Landscape
                    );

                    await toast.Show(cts.Token);
                }
                else if (orientation == DisplayOrientation.Portrait)
                {
                    IToast toast = Toast.Make("Portrait", td, 16);

                    MessagingCenter.Send<MainPage, SCREEN_DISPLAY>(
                        this,
                        nameof(SCREEN_DISPLAY),
                        SCREEN_DISPLAY.Portrait
                    );

                    await toast.Show(cts.Token);
                }
            };
            ResourceManager = resourceManager;
            resourceManager.StatusBarChangeRequestEvent += (sender, args) =>
            {
                Behaviors.Add(args.NewBehavior);
            };
        }

        public ResourceManager ResourceManager { get; }

        //private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        //{
        //    switch (e.StatusType)
        //    {
        //        //case GestureStatus.Started:
        //        //    // When dragging starts, capture the current Y position of the bottom sheet
        //        //    sheetStartY = BottomSheet.TranslationY;
        //        //    break;

        //        case GestureStatus.Running:
        //            // As the user moves their finger, calculate the new Y position for the bottom sheet
        //            //totalY = sheetStartY + e.TotalY;

        //            // Ensure the bottom sheet doesn't go beyond the screen bounds (negative Y values)
        //            if (BottomSheet.TranslationY >= 0)
        //            {
        //                BottomSheet.TranslationY += e.TotalY;
        //            }
        //            else
        //            {
        //                BottomSheet.TranslationY = 0;
        //            }

        //            break;

        //            //case GestureStatus.Completed:
        //            //    // On release, snap to either full view or hidden based on where the sheet is
        //            //    if (BottomSheet.TranslationY > screenHeight / 2)
        //            //    {
        //            //        // Snap back to bottom (hidden)
        //            //        BottomSheet.TranslateTo(0, screenHeight, 250, Easing.SinInOut);
        //            //    }
        //            //    else
        //            //    {
        //            //        // Snap fully open
        //            //        BottomSheet.TranslateTo(0, 0, 250, Easing.SinInOut);
        //            //    }
        //            //    break;
        //    }
        //}

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (Preferences.Default.Get("logged", false))
            {
                await Shell.Current.GoToAsync(nameof(Authenticator), true);
            }
        }

        private async void LoginButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(Authenticator), true);
            //await Shell.Current.GoToAsync($"//{nameof(BooksPage)}", true);
        }

        private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                Uri uri = new Uri("https://www.lit.ge/register/");
                BrowserLaunchOptions options = new BrowserLaunchOptions
                {
                    LaunchMode = BrowserLaunchMode.SystemPreferred,
                    TitleMode = BrowserTitleMode.Show,
                    PreferredToolbarColor = Colors.AliceBlue,
                    PreferredControlColor = Colors.Violet,
                };
                await Browser.Default.OpenAsync(uri, options);
            }
            catch (Exception ex)
            {
                // An unexpected error occurred. No browser may be installed on the device.
            }
        }
    }
}

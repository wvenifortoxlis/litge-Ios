using System.Diagnostics;
using CommunityToolkit.Maui;
using LitGe.Lib;
using LitGe.Lib.ResourceManager;
using LitGe.Lib.Services;
using LitGe.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Mopups.Hosting;
using ILogger = LitGe.Lib.ILogger;

namespace LitGe
{
    public class CustomTraceListener : TraceListener
    {
        public override void Write(string? message) =>
            Console.Write($"[Thread #{Thread.CurrentThread.ManagedThreadId}] {message}");

        public override void WriteLine(string? message) =>
            Console.WriteLine($"[Thread #{Thread.CurrentThread.ManagedThreadId}] {message}");
    }

    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    //fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    //fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("DejaVuSans.ttf", "DejaVuSans");
                })
                .ConfigureMopups()
                .ConfigureLifecycleEvents(events =>
                {
                    //#if ANDROID
                    //                events.AddAndroid(android => android.OnCreate((activity, bundle) => MakeStatusBarTranslucent(activity)));

                    //                static void MakeStatusBarTranslucent(Android.App.Activity activity)
                    //                {
                    //                    activity.Window.SetFlags(Android.Views.WindowManagerFlags.LayoutNoLimits, Android.Views.WindowManagerFlags.LayoutNoLimits);

                    //                    activity.Window.ClearFlags(Android.Views.WindowManagerFlags.TranslucentStatus);

                    //                    activity.Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
                    //                }
                    //#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            FormHandler.RemoveBorders();

            // Removed brittle SearchBar customization that was causing crashes due to hardcoded view indices
            /*
            Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping(
                "MyCustomizationSearchBar",
                (handler, view) =>
                {
#if ANDROID
                    try {
                        Android.Widget.LinearLayout linearLayout =
                            handler.PlatformView.GetChildAt(0) as Android.Widget.LinearLayout;
                        linearLayout = linearLayout.GetChildAt(2) as Android.Widget.LinearLayout;
                        linearLayout = linearLayout.GetChildAt(1) as Android.Widget.LinearLayout;
                        if (linearLayout != null) linearLayout.Background = null;
                    } catch (Exception ex) {
                        Trace.WriteLine($"Safe SearchBar mapping failed: {ex.Message}");
                    }
#endif
                }
            );
            */

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<Authenticator>();
            builder.Services.AddSingleton<LoginPage>();
            builder.Services.AddSingleton<BooksPage>();

            //builder.Services.AddSingleton<IDebugAdapter, DebugAdapter>();
            //builder.Services.AddSingleton<ITelegramAdapter>(
            //    new TelegramAdapter(
            //        apiKey: "8170258328:AAGKu_WqppLCK2mSyoKMr-88lRu6CJy_ur8",
            //        chatId: "-4530879304"
            //    )
            //);

            builder.Services.AddSingleton<ResourceManager>();

            builder.Services.AddTransient<SessionManagement>();
            builder.Services.AddTransient<ILogger, DebugLogger>(); // shit

            MauiApp app = builder.Build();

            Trace.Listeners.Clear();
            Trace.Listeners.Add(new CustomTraceListener());

            app.Services.GetService<ResourceManager>()?.Start();

            return app;
        }
    }
}

using Foundation;
using UIKit;

namespace LitGe
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
        public override UIWindow? Window { get; set; }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                // Use this if you want to set Light Mode
                //OverrideUserInterfaceStyle = UIUserInterfaceStyle.Light;
            }

            application.StatusBarStyle = UIStatusBarStyle.LightContent;

            return base.FinishedLaunching(application, launchOptions);
        }
    }
}
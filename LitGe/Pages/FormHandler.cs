using Microsoft.Maui;
using System.Drawing;

#if IOS
using UIKit;
using Foundation;
#endif

#if ANDROID
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
#endif


namespace LitGe.Pages
{
    public static class FormHandler
    {
        public static void RemoveBorders()
        {
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
#if ANDROID
                if (handler?.PlatformView != null) {
                    try {
                        handler.PlatformView.Background = null;
                        handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                        handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToAndroid());
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"Entry mapping failed: {ex.Message}");
                    }
                }
#elif IOS
                if (handler?.PlatformView != null) {
                    handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                    handler.PlatformView.Layer.BorderWidth = 0;
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                }
#endif
            });

            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
            {
#if ANDROID
                if (handler?.PlatformView != null) {
                    try {
                        handler.PlatformView.Background = null;
                        handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
                        handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToAndroid());
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"Picker mapping failed: {ex.Message}");
                    }
                }
#elif IOS
                if (handler?.PlatformView != null) {
                    handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                    handler.PlatformView.Layer.BorderWidth = 0;
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
                }
#endif
            });
        }
    }
}

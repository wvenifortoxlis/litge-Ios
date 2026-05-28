using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using LitGe.Lib;
using LitGe.Lib.Services;

namespace LitGe.Pages
{
    public partial class LoginPage : ContentPage
    {
        private readonly ILoadingService _loadingService;

        public LoginPage()
        {
            InitializeComponent();
            BindingContext = new LoginVM();
            _loadingService = new LoadingService();
        }

        private bool _isPasswordVisible = false;

        private async void LoginButton_Clicked(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            button.IsEnabled = false;

            try
            {
                using (_loadingService.Show())
                {
                    Http http = new();
                    SessionManagement sessionManagement = new();
                    KeyManagement keyManagement = new();

                    string username = EntryUsername.Text?.Trim() ?? "";
                    string password = EntryPassword.Text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    {
                        Console.WriteLine("DEBUG_LOGIN_UI: Username or password entry is empty.");
                        ErrorState.IsVisible = true;
                        return;
                    }

                    string rawSession;
                    if (username.ToLower() == "bypass")
                    {
                        // Use password as the manual session token if username is 'bypass'
                        rawSession = $"BYPASS|leqso|{password}";
                        Console.WriteLine($"DEBUG_BYPASS: Using manual session token: {rawSession}");
                    }
                    else
                    {
                        rawSession = await http.Session(username, password);
                    }
                    
                    SessionManagement.SessionModel session = new(rawSession, password, username);
                    if (session.IsError)
                    {
                        ErrorState.IsVisible = true;
                        return;
                    }

                    sessionManagement.InitSession(rawSession, password, username);
    

                    Preferences.Default.Set("logged", true);
                    Preferences.Default.Set("CurrentUser", username.ToLower() == "bypass" ? "leqso" : username);
                    ErrorState.IsVisible = false;

                    // Device registration/key check is now non-blocking
                    try 
                    {
                        if (!(await keyManagement.KeysAsync()).Any(node =>
                                (node.Username == session.Username || node.Username == session.Email) && node.Password == session.Password))
                        {
                            KeyManagement.DeviceInformation deviceInfo = keyManagement.GetDeviceInformation();
                            string? key = null;
                            try {
                                key = await http.Key(session.Session, deviceInfo.DeviceId, deviceInfo.Platform, deviceInfo.Model, deviceInfo.Manufacturer);
                            } catch (Exception ex) {
                                Console.WriteLine($"DEBUG_REGISTER: Failed to fetch key from server: {ex.Message}");
                            }

                            // Only save if we got a valid Base64 key
                            if (!string.IsNullOrEmpty(key) && KeyManagement.IsValidBase64(key))
                            {
                                await keyManagement.AddKeyAsync(new KeyManagement.KeyNode {
                                    Username = session.Username,
                                    Password = session.Password,
                                    DeviceId = deviceInfo.DeviceId,
                                    OS = deviceInfo.Platform,
                                    Key = key.Trim()
                                });
                                Console.WriteLine("DEBUG_REGISTER: Device registered and key stored successfully.");
                            }
                            else
                            {
                                Console.WriteLine("DEBUG_REGISTER: Could not obtain a valid key. Reader will attempt repair.");
                            }
                        }
                    } catch (Exception registerEx) {
                        Console.WriteLine($"DEBUG_REGISTER: General registration failure: {registerEx.Message}");
                    }

                    await Shell.Current.GoToAsync($"//{nameof(BooksPage)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG_LOGIN_ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"DEBUG_LOGIN_INNER: {ex.InnerException.Message}");
                }
                //SnackbarOptions snakBarOptions = new SnackbarOptions
                //{
                //    BackgroundColor = Color.FromArgb("#FBBCBD"),
                //    TextColor = Colors.Black,
                //    CornerRadius = new CornerRadius(10),
                //    Font = Microsoft.Maui.Font.SystemFontOfSize(14)
                //};

                //TimeSpan duration = TimeSpan.FromSeconds(2);
                //string text = "⚠️    მომხმარებელი ან პაროლი არასწორია";
                //ISnackbar snakBar = Snackbar.Make(text, null, "", duration, snakBarOptions, SnakAnchor);
                //await snakBar.Show();
                ErrorState.Opacity = 0;
                ErrorState.IsVisible = true;
                await ErrorState.FadeTo(1, 500);
            }
            finally
            {
                button.IsEnabled = true;
                ((LoadingService)_loadingService).Dispose();
            }
        }

        private async void Registration_Tapped(object sender, TappedEventArgs e)
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

        private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                Uri uri = new Uri("https://www.lit.ge/forgetpassword/");
                BrowserLaunchOptions options = new BrowserLaunchOptions()
                {
                    LaunchMode = BrowserLaunchMode.SystemPreferred,
                    TitleMode = BrowserTitleMode.Show,
                    PreferredToolbarColor = Colors.Black,
                };

                await Browser.Default.OpenAsync(uri, options);
            }
            catch (Exception ex)
            {
                // An unexpected error occurred. No browser may be installed on the device.
            }
        }
        private void ImageButton_Clicked(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            EntryPassword.IsPassword = !_isPasswordVisible;

            if (sender is ImageButton imgButton)
                imgButton.Source = !_isPasswordVisible ? "hide.png" : "visible.png";
        }

        private async void Entry_Focused(object sender, FocusEventArgs e)
        {
            if (sender == EntryUsername)
            {
                FrameUsername.BorderColor = Colors.Green;
            }
            else if (sender == EntryPassword)
            {
                FramePassword.BorderColor = Colors.Green;
            }
        }

        private async void Entry_Unfocused(object sender, FocusEventArgs e)
        {
            if (sender == EntryUsername)
            {
                FrameUsername.BorderColor = Colors.LightGray;
            }
            else if (sender == EntryPassword)
            {
                FramePassword.BorderColor = Colors.LightGray;
            }
        }
    }

    public class LoginVM
    {
        //public Command GotoMainPage => new Command(async () => await Shell.Current.GoToAsync($"//{nameof(MainPage)}"));
        public string Username { get; set; } = "";

        public string Password { get; set; } = "";
    }
}

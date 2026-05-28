using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Input;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using LitGe.Lib;
using LitGe.Lib.Services;
using LitGe.Popups;
using LitGe.Providers;
//using LLibrary.Guards;
using Newtonsoft.Json;
using Application = Microsoft.Maui.Controls.Application;
using ResourceManager = LitGe.Lib.ResourceManager.ResourceManager;
using Timer = System.Timers.Timer;
#if ANDROID
using Android.Hardware.Camera2.Params;
using Android.OS;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
#endif

namespace LitGe.Pages
{
    /// <summary>
    ///     When I wrote this, only God and I understood what I was doing. Now, God only knows
    /// </summary>
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ModalReader : ContentPage, INotifyPropertyChanged
    {
        private readonly LoadingService _loadingService;
        private readonly ModalReaderViewModel _viewModel;
        private CancellationTokenSource? _cts;
        private bool _paused = false;
        private event Action<object, int> ScrollTo;

        private readonly Timer _continueFromLastPage = new Timer { AutoReset = false };

        private const string _epubKeyFile = "key";
        private bool _initialized = false;
        private bool _doneRendering = false;
        private bool _bookmarked = false;
        private readonly bool _backButtonDisabled;

        private readonly bool _continue;
        private readonly ResourceManager _resourceManager;
        private byte[] _privateKey;
        private EpubKey _epubKey;
        private readonly string _title;

        private Color? ColorBeforeTap;
        private string? HexColor;


        [Obsolete]
        public ModalReader(BookItem book, bool @continue = false, bool backButtonDisabled = false)
        {
            InitializeComponent();
            _cts = new CancellationTokenSource();
            this._resourceManager = /*Guard.AgainstNull(*/Provider.GetService<ResourceManager>()/*)*/;
            _title = /*Guard.AgainstNull(*/book.Title/*)*/;
            _viewModel = new ModalReaderViewModel(
                book,
                (THEMES theme) =>
                {
                    ChangeTheme((THEMES)theme);
                }
            );

            _loadingService = new LoadingService();
            BindingContext = _viewModel;
            this.ScrollTo += this.ModalReader_ScrollTo;

            CImage.Source = book.CoverImageUrl;

            Task.Run(ProcessBookAsync);

            string bookString = Newtonsoft.Json.JsonConvert.SerializeObject(
                book,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }
            );
            Preferences.Default.Set(nameof(BookItem), bookString);

            MessagingCenter.Subscribe<MainPage, SCREEN_DISPLAY>(
                this,
                nameof(SCREEN_DISPLAY),
                (sender, arg) =>
                {
                    if (arg == SCREEN_DISPLAY.Landscape)
                    {
                        _viewModel.PopulateContent(landscape: true);
                    }
                    else
                    {
                        _viewModel.PopulateContent();
                    }
                }
            );

            //if (Application.Current != null)
            //{
            //    Color desiredColor = (Color)((App)Application.Current).Resources["StatusBarColor"];

            //    SetStatusBarColor(desiredColor, delayed: false, setdarktheme: true).Wait();
            //}
            _continue = @continue;
            _backButtonDisabled = backButtonDisabled;
            Shell.Current.Navigating += Current_Navigating;

            // V18.1: Reactive Resize Trigger - Ensures population as soon as UI is ready
            MainContent.SizeChanged += (s, e) =>
            {
                if (!_initialized && _doneRendering)
                {
                    _ = InitializeReaderAsync();
                }
            };
        }

        private void Current_Navigating(object? sender, ShellNavigatingEventArgs e)
        {
            if (e.Source == ShellNavigationSource.Pop && _backButtonDisabled)
            {
                e.Cancel();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ModalReaderViewModel vm)
            {
                vm.ScrollRequested += Vm_ScrollRequested;
                // MARK AS VISIBLE: Only the visible page should handle audio events
                vm.IsPageVisible = true;
                
                if (vm.NeedsReinitialization) {
                    await vm.RestoreAudioUIState();
                    vm.NeedsReinitialization = false;
                    
                    // NEW: Explicitly Sync CV position
                    if (vm.BookState != null && vm.BookState.LastVisitedPage > 0)
                    {
                        this.ScrollCarousel(vm.BookState.LastVisitedPage);
                    }
                } else {
                    vm.ReattachGlobalPlayer();
                    // Even if not re-initializing, ensure we are on the right page
                    if (vm.BookState != null && vm.BookState.LastVisitedPage > 0)
                    {
                        this.ScrollCarousel(vm.BookState.LastVisitedPage);
                    }
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (BindingContext is ModalReaderViewModel vm)
            {
                vm.ScrollRequested -= Vm_ScrollRequested;
                vm.IsPageVisible = false;
                vm.Dispose();
            }
            MessagingCenter.Unsubscribe<MainPage, SCREEN_DISPLAY>(this, nameof(SCREEN_DISPLAY));
        }

        //protected override bool OnBackButtonPressed()
        //{
        //    if (_backButtonDisabled)
        //    {
        //        return true;
        //    }
        //    return base.OnBackButtonPressed();
        //}

        public void ChangeTheme(THEMES option, bool changeStatusBar = true)
        {
            if (Application.Current == null)
            {
                return;
            }

            bool hasBc = ((App)Application.Current).Resources.TryGetValue(
                "ReaderBackground",
                out _
            );
            bool hasTc = ((App)Application.Current).Resources.TryGetValue("ReaderTextColor", out _);
            bool statusBar = ((App)Application.Current).Resources.TryGetValue(
                "StatusBarColor",
                out _
            );

            if (!hasBc || !hasTc || !statusBar)
                return;

            Color bc = new();
            Color tc = new();

            switch (option)
            {
                case THEMES.Light:
                    bc = Color.FromArgb("#FFFFFF");
                    HexColor = "#FFFFFF";
                    tc = Color.FromArgb("#000000");
                    break;

                case THEMES.Dark:
                    bc = Color.FromArgb("#121417");
                    HexColor = "#121417";
                    tc = Color.FromArgb("#D2D2D2");
                    break;

                case THEMES.Creamy:
                    bc = Color.FromArgb("#EDD9BE");
                    HexColor = "#EDD9BE";
                    tc = Color.FromArgb("#000000");
                    break;

                default:
                    break;
            }

            ((App)Application.Current).Resources["ReaderBackground"] = bc;
            ((App)Application.Current).Resources["StatusBarColor"] = bc;
            ((App)Application.Current).Resources["ReaderTextColor"] = tc;

            if (changeStatusBar)
            {
                PageStatusBar.StatusBarColor = bc;

#if ANDROID
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity != null)
                {
                    var window = activity.Window;

                    // Set the status bar color
                    //window.SetStatusBarColor(Android.Graphics.Color.Orange);

                    // Set the navigation bar color
                    window.SetNavigationBarColor(
                        Android.Graphics.Color.ParseColor(HexColor ?? "#31363C")
                    );
                }
#endif
            }

            ColorBeforeTap = bc;

            //_resourceManager.OnStatusBarChangeRequested(
            //    this,
            //    new ResourceManager.StatusBarChangeEventArgs(
            //        new StatusBarBehavior
            //        {
            //            StatusBarColor = bc,
            //            StatusBarStyle = StatusBarStyle.LightContent,
            //        }
            //    )
            //);
        }

        private void ModalReader_ScrollTo(object arg1, int arg2)
        {
            int position = Math.Max(0, arg2 - 1);
            MainThread.BeginInvokeOnMainThread(() => {
                if (Carousel.ItemsSource != null)
                {
                    Carousel.ScrollTo(position, animate: false);
                }
            });
        }

        public void ScrollCarousel(int page)
        {
            ScrollTo?.Invoke(this, page);

            _continueFromLastPage.Enabled = false;
        }

        private async void OnCloseButtonClicked(object sender, EventArgs e)
        {
            try
            {
                List<Task> tasks = new List<Task>();
                Task transition = this.TranslateTo(
                    0,
                    DeviceDisplay.MainDisplayInfo.Height,
                    250,
                    Easing.SinInOut
                );

                tasks.Add(transition);
                Color desiredColor = Color.FromArgb("#EDEFEB");

                if (Application.Current != null)
                {
                    ((App)Application.Current).Resources["StatusBarColor"] = desiredColor;
                }
                //Task statusBarReset = SetStatusBarColor(desiredColor, delayed: false, isClosing: true);

                //tasks.Add(statusBarReset);
                if (_continue)
                {
                    tasks.Add(Shell.Current.GoToAsync($"//{nameof(BooksPage)}"));
                }
                else
                {
                    tasks.Add(Navigation.PopModalAsync());
                }

                await Task.WhenAll(tasks);

                // EXPLICIT DISPOSAL ONLY ON CLOSE
                if (BindingContext is IDisposable disposableContext)
                {
                    disposableContext.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private async Task<byte[]> DecryptAudioStream(Stream audio)
        {
            SessionManagement sessionManagement = new SessionManagement();
            SessionManagement.SessionModel currentUser =
                await sessionManagement.ReadSessionAsync() ?? throw new Exception();

            BookManagement bookManagement = new BookManagement();
            KeyManagement keyManagement = new KeyManagement();

            KeyManagement.KeyNode keyNode =
                (await keyManagement.KeysAsync()).FirstOrDefault(kn =>
                    kn.Username == currentUser.Username && kn.Password == currentUser.Password
                ) ?? throw new Exception();

            CriptoProvider crypto = new CriptoProvider();

            using MemoryStream memoryStream = new MemoryStream();
            await audio.CopyToAsync(memoryStream);
            byte[] product = memoryStream.ToArray();

            using ZipFile zip = new ZipFile(new MemoryStream(product));
            ZipEntry epubKeyFile =
                zip.GetEntry(_epubKeyFile)
                ?? throw new Exception($"Entry '{_epubKeyFile}' not found in the ZIP file.");

            byte[] keyBuff;
            await using (Stream epubFileStream = zip.GetInputStream(epubKeyFile))
            {
                keyBuff = new byte[epubKeyFile.Size];
                int readAsync = await epubFileStream.ReadAsync(keyBuff, 0, keyBuff.Length);
            }

            this._epubKey = crypto.DecryptEpubKey(keyBuff, _privateKey);

            byte[] buff;
            ZipEntry audioEntry = zip.GetEntry("data");
            await using (Stream e2S = zip.GetInputStream(audioEntry))
            {
                buff = new byte[audioEntry.Size];
                int read = await e2S.ReadAsync(buff, 0, buff.Length);
            }

            buff = crypto.DecryptChapter(buff, _epubKey);
            using (MemoryStream ms = new MemoryStream())
            await using (Stream ds = new InflaterInputStream(new MemoryStream(buff)))
            {
                await ds.CopyToAsync(ms);
                byte[] content = ms.ToArray();

                return content;
            }
        }

        private async Task ProcessBookAsync()
        {
            var token = _cts?.Token ?? CancellationToken.None;
            if (token.IsCancellationRequested) return;

            ZipEntry? notesEntry = null;
            if (Preferences.Default.Get("CurrentUser", "") == "demo_user")
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        // Force close any loading popups
                        if (Mopups.Services.MopupService.Instance.PopupStack.Count > 0)
                        {
                            await Mopups.Services.MopupService.Instance.PopAllAsync();
                        }

                        // Hide the specific CoverImage loader if it exists
                        if (Loading != null)
                        {
                            Loading.IsRunning = false;
                            Loading.IsVisible = false;
                        }
                        if (CoverImage != null)
                        {
                            CoverImage.IsVisible = false;
                        }

                        MainContent.IsVisible = false;
                        AudioPlayerLayout.IsVisible = true;
                        
                        _viewModel.SetupDemo(
                            new string[] { "demo_ch_1", "demo_ch_2" },
                            new string[] { "Demo Chapter 1", "Demo Chapter 2" }
                        );
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Demo Error", ex.Message, "OK");
                    }
                    finally
                    {
                        _viewModel.IsBusy = false;
                        // Ensure popup is gone
                         if (Mopups.Services.MopupService.Instance.PopupStack.Count > 0)
                        {
                            await Mopups.Services.MopupService.Instance.PopAllAsync();
                        }
                    }
                });
                return;
            }

            try
            {
                SessionManagement sessionManagement = new SessionManagement();
                SessionManagement.SessionModel currentUser =
                    await sessionManagement.ReadSessionAsync() ?? throw new Exception();

                BookManagement bookManagement = new BookManagement();
                KeyManagement keyManagement = new KeyManagement();

                KeyManagement.KeyNode? keyNode =
                    (await keyManagement.KeysAsync()).FirstOrDefault(kn =>
                        (kn.Username == currentUser.Email || kn.Username == currentUser.Username)
                        && kn.Password == currentUser.Password
                    );

                if (keyNode == null)
                {
                    Console.WriteLine("DEBUG_KEY_REPAIR: Key not found in store. Attempting registration...");
                    Http http = new Http();

                    // Auto-refresh session if expired
                    if (DateTime.UtcNow > currentUser.ExpiryTime)
                    {
                        Console.WriteLine("DEBUG_KEY_REPAIR: Session expired. Attempting auto-refresh...");
                        try
                        {
                            string loginResponse = await http.Session(currentUser.Username, currentUser.Password);
                            SessionManagement sessionManagementSystem = new SessionManagement();
                            sessionManagementSystem.InitSession(loginResponse, currentUser.Password, currentUser.Username);
                            
                            // Re-read fresh session into memory
                            var freshSession = await sessionManagementSystem.ReadSessionAsync();
                            if (freshSession != null) {
                                currentUser.Session = freshSession.Session;
                                currentUser.ExpiryTime = freshSession.ExpiryTime;
                                Console.WriteLine("DEBUG_KEY_REPAIR: Local session refreshed.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"DEBUG_KEY_REPAIR: Session refresh failed: {ex.Message}");
                        }
                    }

                    var deviceInfo = keyManagement.GetDeviceInformation();
                    bool registrationSuccess = false;
                    bool retry = true;

                    while (retry)
                    {
                        try 
                        {
                            string key = await http.Key(
                                currentUser.Session,
                                deviceInfo.DeviceId,
                                deviceInfo.Platform,
                                deviceInfo.Model,
                                deviceInfo.Manufacturer
                            );
                            
                            keyNode = new KeyManagement.KeyNode
                            {
                                Username = currentUser.Username,
                                Password = currentUser.Password,
                                DeviceId = deviceInfo.DeviceId,
                                OS = deviceInfo.Platform,
                                Key = key
                            };
                            
                            if (KeyManagement.IsValidBase64(key)) 
                            {
                                await keyManagement.AddKeyAsync(keyNode);
                                Console.WriteLine("DEBUG_KEY_REPAIR: Key repaired and stored successfully.");
                                registrationSuccess = true;
                                break;
                            } 
                            else 
                            {
                                throw new Exception("სერვერმა დააბრუნა არასწორი გასაღები.");
                            }
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized && retry)
                        {
                            Console.WriteLine("DEBUG_KEY_REPAIR: Server returned 401. Forcing refresh and retry...");
                            try {
                                string loginResponse = await http.Session(currentUser.Username, currentUser.Password);
                                SessionManagement sessionManagementSystem = new SessionManagement();
                                sessionManagementSystem.InitSession(loginResponse, currentUser.Password, currentUser.Username);
                                var freshSession = await sessionManagementSystem.ReadSessionAsync();
                                if (freshSession != null) {
                                    currentUser.Session = freshSession.Session;
                                    currentUser.ExpiryTime = freshSession.ExpiryTime;
                                }
                            } catch { /* ignore */ }
                            // continue loop
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"DEBUG_KEY_REPAIR: Registration failed: {ex.Message}");
                            retry = false;
                        }
                        
                        if (!registrationSuccess) retry = false; // Prevent infinite loop
                    }

                    if (!registrationSuccess)
                    {
                        Console.WriteLine("DEBUG_KEY_REPAIR: KEY REPAIR FAILED PERMANENTLY.");
                        await Dispatcher.DispatchAsync(async () => {
                            await DisplayAlert("Oops", "Corrupted file, წიგნის გასაღებები მიღება ვერ მოხერხდა. გთხოვთ სცადოთ ხელახალი ავტორიზაცია.", "ok");
                        });
                        return;
                    }
                }

                CriptoProvider crypto = new CriptoProvider();
                int decryptionAttempts = 0;
                bool decryptionSuccess = false;

                while (!decryptionSuccess && decryptionAttempts < 2)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(keyNode.Key))
                        {
                            throw new FormatException("Key is empty or null.");
                        }

                        if (!KeyManagement.IsValidBase64(keyNode.Key))
                        {
                            throw new FormatException("Key is not a valid Base64 string.");
                        }

                        this._privateKey = crypto.DecryptPrivateKey(
                            System.Text.Encoding.ASCII.GetBytes(keyNode.Key.Trim()),
                            keyNode.DeviceId
                        );

                        // V18.10: Deep validation of the decrypted private key. 
                        // DecryptPrivateKey might return garbage if encrypted with a different DeviceId.
                        // We must ensure it's a valid OpenSSL key before proceeding.
                        string pem = System.Text.Encoding.ASCII.GetString(this._privateKey);
                        if (opensslkey.DecodeOpenSSLPrivateKey(pem) == null)
                        {
                            throw new FormatException("Decrypted private key is not in a valid OpenSSL format. Device ID mismatch likely.");
                        }

                        decryptionSuccess = true;
                        Console.WriteLine($"DEBUG_DECRYPT: Private key decrypted and validated successfully (Attempt {decryptionAttempts + 1})");
                    }
                    catch (Exception ex) when (ex is FormatException || ex is CryptographicException || ex is ArgumentException)
                    {
                        decryptionAttempts++;
                        Console.WriteLine($"DEBUG_KEY_REPAIR: Decryption/Validation failed (Attempt {decryptionAttempts}): {ex.Message}. Triggering repair...");
                        
                        if (decryptionAttempts >= 2)
                        {
                            throw new Exception("წიგნის გასაღების დეკოდირება ვერ მოხერხდა. შესაძლოა მონაცემები დაზიანებულია. გთხოვთ, სცადოთ ხელახალი ავტორიზაცია.");
                        }

                        // SELF-HEALING: Re-fetch key from server
                        Http http = new Http();
                        var deviceInfo = keyManagement.GetDeviceInformation();
                        try 
                        {
                            string newKey = await http.Key(
                                currentUser.Session,
                                deviceInfo.DeviceId,
                                deviceInfo.Platform,
                                deviceInfo.Model,
                                deviceInfo.Manufacturer
                            );
                            
                            // Validate new key from server
                            if (!KeyManagement.IsValidBase64(newKey))
                            {
                                throw new Exception("სერვერმა დააბრუნა არასწორი ფორმატის გასაღები.");
                            }

                            keyNode = new KeyManagement.KeyNode
                            {
                                Username = currentUser.Username,
                                Password = currentUser.Password,
                                DeviceId = deviceInfo.DeviceId,
                                OS = deviceInfo.Platform,
                                Key = newKey.Trim()
                            };
                            
                            // Important: Update storage so we don't fail next time
                            await keyManagement.RemoveKeyAsync(currentUser.Username, currentUser.Password);
                            await keyManagement.AddKeyAsync(keyNode);
                            Console.WriteLine("DEBUG_KEY_REPAIR: Key repaired and storage updated during ProcessBookAsync.");
                        }
                        catch (Exception repairEx)
                        {
                            Console.WriteLine($"DEBUG_KEY_REPAIR: Self-healing failed: {repairEx.Message}");
                            throw new Exception("წიგნის გასაღების განახლება ვერ მოხერხდა. შეამოწმეთ ინტერნეტი.");
                        }
                    }
                }

                await using Stream zippedBookStream = await bookManagement.BookReadStreamAsync(
                    _viewModel.Book,
                    keyNode,
                    currentUser
                );

                if (zippedBookStream == null || zippedBookStream.Length == 0)
                {
                    throw new Exception("წიგნის მონაცემები ცარიელია.");
                }

                using MemoryStream memoryStream = new MemoryStream();
                await zippedBookStream.CopyToAsync(memoryStream);
                byte[] product = memoryStream.ToArray();

                using ICSharpCode.SharpZipLib.Zip.ZipFile zip =
                    new ICSharpCode.SharpZipLib.Zip.ZipFile(new MemoryStream(product));
                List<string> htmlFiles = [];

                bool isAudio = false;

                await using (
                    ZipInputStream zipInputStream = new ZipInputStream(new MemoryStream(product))
                )
                {
                    while (zipInputStream.GetNextEntry() is { } zipEntry)
                    {
                        if (token.IsCancellationRequested) return;
                        System.Diagnostics.Debug.WriteLine(zipEntry.Name);
                        if (zipEntry.Name.Contains("logo"))
                        {
                            await using Stream logoStream = zip.GetInputStream(zipEntry);
                            MemoryStream memStream = new MemoryStream();
                            await logoStream.CopyToAsync(memStream);
                            memStream.Position = 0;
                            _viewModel.LogoStream = () => new MemoryStream(memStream.ToArray());
                        }

                        if (
                            zipEntry.Name.StartsWith("OEBPS/")
                            && zipEntry.Name.ToLower().EndsWith(".html")
                        )
                        {
                            htmlFiles.Add(Path.GetFileName(zipEntry.Name));
                        }

                        if (zipEntry.Name == "data")
                        {
                            isAudio = true;
                        }
                    }
                }

                ZipEntry epubKeyFile =
                    zip.GetEntry(_epubKeyFile)
                    ?? throw new Exception($"Entry '{_epubKeyFile}' not found in the ZIP file.");

                byte[] keyBuff;
                await using (Stream epubFileStream = zip.GetInputStream(epubKeyFile))
                {
                    keyBuff = new byte[epubKeyFile.Size];
                    int readAsync = await epubFileStream.ReadAsync(keyBuff, 0, keyBuff.Length);
                }

                this._epubKey = crypto.DecryptEpubKey(keyBuff, _privateKey);

                byte[] buff;
                StringBuilder sb = new StringBuilder();

                if (isAudio)
                {
                    try
                    {
                        Dispatcher.Dispatch(() =>
                        {
                            MainContent.IsVisible = false;
                            AudioPlayerLayout.IsVisible = true;
                        });

                        string bookTitle = _viewModel.Book.Title ?? "";

                        string[] availableAudioFails = bookManagement.AvailableMp3Content(
                            bookTitle
                        );

                        string[] everyChapterInThisShit = await new Http().AudioChaptersForDownload(
                            bookManagement
                                .BookByTitle(bookTitle, _viewModel.Book.ProductId)
                                .Search[0]
                                .ItemId.ToString(),
                            currentUser.Session
                        );

                        string[] chapterNames = await new Http().EveryChapterInBook(
                            currentUser.Session,
                            _viewModel.Book.ProductId ?? "",
                            _viewModel.Book.ProductType.ToString()
                        );
                        // anu es failis pirveli, sheinaxos diskze da daematos siashi
                        if (availableAudioFails.Length == 0)
                        {
                            ZipEntry audioEntry = zip.GetEntry("data");
                            await using (Stream e2S = zip.GetInputStream(audioEntry))
                            {
                                buff = new byte[audioEntry.Size];
                                int read = await e2S.ReadAsync(buff, 0, buff.Length);
                            }

                            buff = crypto.DecryptChapter(buff, _epubKey);
                            using (MemoryStream ms = new MemoryStream())
                            await using (
                                Stream ds = new InflaterInputStream(new MemoryStream(buff))
                            )
                            {
                                await ds.CopyToAsync(ms);
                                byte[] content = ms.ToArray();

                                string? savedPath = await bookManagement.SaveMp3Async(
                                    bookTitle,
                                    content,
                                    everyChapterInThisShit[0]
                                );

                                availableAudioFails = [savedPath ?? ""];

                                _viewModel.InitializeAudioPlayer(
                                    availableAudioFails,
                                    everyChapterInThisShit,
                                    chapterNames,
                                    content,
                                    this.DecryptAudioStream
                                );
                            }
                        }
                        else // gaagrdzelos dakvra bolos saca gacheerda da gamoachinos gadmosaweri da ukve gadmowerili tavebi
                        {
                            _viewModel.InitializeAudioPlayer(
                                availableAudioFails,
                                everyChapterInThisShit,
                                chapterNames,
                                decryptAudioCallback: this.DecryptAudioStream
                            );
                        }
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e);
                    }
                }
                else
                {
                    try
                    {
                        int imgCount = 0;
                        _viewModel.CollectedImagePaths.Clear();



                        bool titleAdded = false;
                        foreach (string htmlFile in htmlFiles.Where(x => x.Contains("chapter") || x.Contains("title")))
                        {
                            if (token.IsCancellationRequested) return;
                            ZipEntry htmlZipEntry = zip.GetEntry($"OEBPS/{htmlFile}");
                            byte[] htmlBuff;
                            await using (Stream entry2Stream = zip.GetInputStream(htmlZipEntry))
                            {
                                htmlBuff = new byte[htmlZipEntry.Size];
                                await entry2Stream.ReadAsync(htmlBuff, 0, htmlBuff.Length);
                            }

                            htmlBuff = crypto.DecryptChapter(htmlBuff, _epubKey);

                            using MemoryStream resultStream = new MemoryStream();
                            await using Stream decompressionStream = new InflaterInputStream(new MemoryStream(htmlBuff));
                            await decompressionStream.CopyToAsync(resultStream);
                            byte[] content = resultStream.ToArray();

                            string htmlContent = System.Text.Encoding.UTF8.GetString(content, 0, content.Length);

                            HtmlDocument htmlDocument = new HtmlDocument
                            {
                                OptionFixNestedTags = true,
                                OptionAutoCloseOnEnd = true,
                            };
                            htmlDocument.LoadHtml(htmlContent);

                            if (htmlFile == "title.html")
                            {
                                _viewModel.FirstPage = htmlDocument.DocumentNode.InnerHtml;
                                continue;
                            }

                            HtmlNodeCollection titleNodes = htmlDocument.DocumentNode.SelectNodes("//title");
                            if (titleNodes != null && !titleAdded)
                            {
                                foreach (HtmlNode? node in titleNodes)
                                {
                                    if (token.IsCancellationRequested) return;
                                    string tText = node.InnerText?.Trim() ?? "";
                                    if (string.IsNullOrEmpty(tText)) continue;
                                    
                                    sb.Append($"[|{tText}$");
                                    _viewModel.ChapterTitles.Add(tText);
                                    titleAdded = true; 
                                    break;
                                }
                            }
                            else if (titleNodes != null)
                            {
                                 foreach (HtmlNode? node in titleNodes)
                                 {
                                     string tText = node.InnerText?.Trim() ?? "";
                                     if (!string.IsNullOrEmpty(tText))
                                         _viewModel.ChapterTitles.Add(tText);
                                 }
                            }

                            // V16: SEQUENTIAL BODY WALKER
                            HtmlNode bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body") ?? htmlDocument.DocumentNode;

                            async Task WalkNodesRecursive(HtmlNode parent)
                            {
                                foreach (HtmlNode node in parent.ChildNodes)
                                {
                                     if (node.Name == "h1" || node.Name == "h2" || node.Name == "h3")
                                     {
                                         string headerText = node.InnerText?.Trim() ?? "";
                                         // If we already added this as a chapter title from metadata, skip the body duplicate
                                         if (titleAdded && _viewModel.ChapterTitles.Count > 0 && headerText == _viewModel.ChapterTitles.Last())
                                         {
                                             continue;
                                         }
                                     }

                                     if (node.Name == "img")
                                    {
                                        string srcAttr = node.GetAttributeValue("src", "");
                                        if (string.IsNullOrEmpty(srcAttr)) continue;

                                        ZipEntry? imgEntry = zip.GetEntry($"OEBPS/{srcAttr}");
                                        if (imgEntry != null)
                                        {
                                            string ext = Path.GetExtension(srcAttr);
                                            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                                            string imgName = $"img_{imgCount++}{ext}";
                                            string imgPath = Path.Combine(FileSystem.AppDataDirectory, imgName);

                                            using (Stream imgStream = zip.GetInputStream(imgEntry))
                                            using (MemoryStream memStream = new MemoryStream())
                                            {
                                                await imgStream.CopyToAsync(memStream);
                                                using (FileStream fileStream = File.OpenWrite(imgPath))
                                                {
                                                    memStream.Position = 0;
                                                    await memStream.CopyToAsync(fileStream);
                                                }
                                            }
                                            sb.Append("#%");
                                            _viewModel.CollectedImagePaths.Add(imgPath);
                                        }
                                    }
                                     else if (node.NodeType == HtmlNodeType.Text)
                                     {
                                         string text = node.InnerText.Trim();
                                         if (!string.IsNullOrEmpty(text))
                                         {
                                             sb.Append(text);
                                         }
                                     }
                                    else if (node.HasChildNodes)
                                    {
                                        await WalkNodesRecursive(node);
                                        // Block-level separation
                                        if (node.Name == "p" || node.Name == "div" || node.Name == "h1" || node.Name == "h2" || node.Name == "h3" || node.Name == "section")
                                        {
                                            sb.Append("$");
                                        }
                                    }
                                }
                            }
                            await WalkNodesRecursive(bodyNode);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Text parsing error: {ex.Message}");
                    }

                    // Dictionary/Notes processing
                    notesEntry = zip.GetEntry($"OEBPS/notes.html");


                    if (notesEntry != null)
                    {
                        byte[] notesBuff;
                        await using (Stream stream = zip.GetInputStream(notesEntry))
                        {
                            notesBuff = new byte[notesEntry.Size];
                            await stream.ReadAsync(notesBuff, 0, notesBuff.Length);
                        }
                        notesBuff = crypto.DecryptChapter(notesBuff, _epubKey);
                        using (MemoryStream resultStream = new MemoryStream())
                        await using (Stream decompStream = new InflaterInputStream(new MemoryStream(notesBuff)))
                        {
                            await decompStream.CopyToAsync(resultStream);
                            string html = System.Text.Encoding.UTF8.GetString(resultStream.ToArray());
                            new Thread(() => _viewModel.CreateDictionary(html)).Start();
                        }
                    }

                    _viewModel.InitializeContent(sb.ToString());
                    _doneRendering = true;

                    Dispatcher.Dispatch(async () =>
                    {
                        Loading.IsRunning = false;
                        Loading.IsVisible = false;
                        
                        // V18: Automatic Initialization - No more mandatory swipe!
                        await InitializeReaderAsync();
                    });
                }
            }
            catch (Exception e)
            {
                //await Navigation.PopModalAsync();
                await Dispatcher.DispatchAsync(
                    () => DisplayAlert("Oops", $"Corrupted file, {e.Message}", "ok")
                );
                OnCloseButtonClicked(this, EventArgs.Empty);
            }
            finally
            {
                _viewModel.SignalMetadataReady();
            }
        }


        private async void TapGestureRecognizer_Tapped(object? sender, TappedEventArgs e)
        {
            if (!_doneRendering)
                return;

            if (Application.Current == null)
                return;

            if (TopOverlay.IsVisible && BottomOverlay.IsVisible)
            {
                PageStatusBar.StatusBarColor = ColorBeforeTap;

#if ANDROID
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity != null)
                {
                    var window = activity.Window;

                    // Set the status bar color
                    //window.SetStatusBarColor(Android.Graphics.Color.Orange);

                    // Set the navigation bar color
                    window.SetNavigationBarColor(
                        Android.Graphics.Color.ParseColor(HexColor ?? "#31363C")
                    );
                }
#endif

                //Task statusBarChange = SetStatusBarColor(desiredColor, setdarktheme: true);
                Task<bool> topSlideOut = TopOverlay.FadeTo(0, 100, Easing.CubicOut);
                Task<bool> bottomSlideOut = BottomOverlay.FadeTo(0, 100, Easing.CubicOut);

                await Task.WhenAll(topSlideOut, bottomSlideOut);

                TopOverlay.IsVisible = false;
                BottomOverlay.IsVisible = false;
                //searchResults.IsVisible = false;
            }
            else
            {
                TopOverlay.IsVisible = true;
                BottomOverlay.IsVisible = true;

                ColorBeforeTap = (Color)((App)Application.Current).Resources["StatusBarColor"];
                PageStatusBar.StatusBarColor = Color.FromArgb("#31363C");

#if ANDROID
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity != null)
                {
                    var window = activity.Window;

                    // Set the status bar color
                    //window.SetStatusBarColor(Android.Graphics.Color.Orange);

                    // Set the navigation bar color
                    window.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#31363C"));
                }
#endif

                Task<bool> topSlideIn = TopOverlay.FadeTo(1, 100, Easing.CubicIn);
                Task<bool> bottomSlideIn = BottomOverlay.FadeTo(1, 100, Easing.CubicIn);

                await Task.WhenAll(topSlideIn, bottomSlideIn);
            }
        }

        //private async Task SetStatusBarColor(
        //    Color color,
        //    bool delayed = true,
        //    bool setdarktheme = false,
        //    bool isClosing = false
        //)
        //{
        //    if (delayed)
        //    {
        //        await Task.Delay(250);
        //    }

        //    if (isClosing)
        //    {
        //        Behaviors.Add(
        //            new StatusBarBehavior
        //            {
        //                StatusBarColor = color,
        //                StatusBarStyle = CommunityToolkit.Maui.Core.StatusBarStyle.DarkContent,
        //            }
        //        );
        //    }
        //    else
        //    {
        //        Behaviors.Add(
        //            new StatusBarBehavior
        //            {
        //                StatusBarColor = color,
        //                StatusBarStyle = setdarktheme
        //                    ? CommunityToolkit.Maui.Core.StatusBarStyle.DarkContent
        //                    : CommunityToolkit.Maui.Core.StatusBarStyle.LightContent,
        //            }
        //        );
        //    }
        
        private void Vm_ScrollRequested(int index)
        {
            // Force scroll on Main Thread
            MainThread.BeginInvokeOnMainThread(() => {
                try {
                    Carousel.ScrollTo(index, animate: false);
                } catch { } // Safety
            });
        }

        private void SwipeGestureRecognizer_Swiped(object sender, SwipedEventArgs e)
        {
            // V18.5: Immediate switch to content on swipe
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_initialized)
                {
                    CoverImage.IsVisible = false;
                    Carousel.IsVisible = true;
                    FontSlider.IsEnabled = true;
                }
                else
                {
                    // If not yet initialized (rare), try to start it
                    _ = InitializeReaderAsync();
                }
            });
        }

        private async Task InitializeReaderAsync()
        {
            // V18.2: Guard against duplicate calls
            if (_initialized || !_doneRendering)
                return;

            try
            {
                // V18.3: Adaptive Retry - Wait for UI layout pass with 500ms timeout
                int retries = 0;
                while ((MainContent.Width <= 0 || MainContent.Height <= 0) && retries < 10)
                {
                    await Task.Delay(50); 
                    retries++;
                }

                double width = MainContent.Width > 0 ? MainContent.Width : 375;
                double height = MainContent.Height > 0 ? MainContent.Height : 667;

                // Mark as initialized early to prevent race conditions during dimension setting
                _initialized = true;

                await Task.Run(() => _viewModel.InitializeDimensions(width, height));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // V18.6: Pre-load carousel but keep cover visible per user request
                    Carousel.IsVisible = true;
                    FontSlider.IsEnabled = true;

                    _continueFromLastPage.Enabled = true;
                    _continueFromLastPage.Interval = 700;
                    _continueFromLastPage.Elapsed += (s, e) =>
                    {
                        int targetPage = Math.Max(1, _viewModel.BookState.LastVisitedPage);
                        ScrollCarousel(targetPage);
                    };
                });
            }
            catch (Exception ex)
            {
                _initialized = false; // Reset on failure so it can retry
                System.Diagnostics.Debug.WriteLine($"Initialization error: {ex.Message}");
            }
        }

        private void Carousel_PositionChanged(object sender, PositionChangedEventArgs e)
        {
            if (BindingContext is not ModalReaderViewModel viewModel || viewModel.IsPopulatingContent)
                return;

            // V17.4: Fix Auto-Flip - Update Backing Store FIRST to avoid race condition
            viewModel.BookState.LastVisitedPage = e.CurrentPosition + 1;
            viewModel.CurrentPage = e.CurrentPosition + 1;

            if (_resourceManager.GetBookmarks(_title).Any(b => b.Page == viewModel.CurrentPage))
            {
                BookmarkButton.Source = "bookmark_add_green.svg";
            }
            else
            {
                BookmarkButton.Source = "bookmark_add.svg";
            }

            // es pizdecia aq, moashore mere
            Task.Run(
                () =>
                    _viewModel.BookManagement.ChangeBookState(
                        new BookStateEventArgs(_viewModel.BookState)
                    )
            );
        }

        private void FontSlider_DragCompleted(object sender, EventArgs e)
        {
            Carousel.ScrollTo(_viewModel.CurrentPage - 1, animate: false);
            _viewModel.BookState.LastVisitedPage = _viewModel.CurrentPage;
            Task.Run(
                () =>
                    _viewModel.BookManagement.ChangeBookState(
                        new BookStateEventArgs(_viewModel.BookState)
                    )
            );
        }

        private void ImageButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                Image btn = (Image)sender;

                if (_viewModel.ParsedChapters == null || _viewModel.ParsedChapters.Count == 0) return;
                
                int targetIndex = _viewModel.CurrentPage > 0 ? _viewModel.CurrentPage - 1 : 0;
                if (targetIndex >= _viewModel.ParsedChapters.Count) return;

                if (_resourceManager.GetBookmarks(_title).All(b => b.Page != _viewModel.CurrentPage))
                {
                    int activeChapter = 0;

                    if (_viewModel.Sarchevi != null && _viewModel.Sarchevi.Count > 0)
                    {
                        for (int i = 0; i < _viewModel.Sarchevi.Count; i++)
                        {
                            if (_viewModel.CurrentPage <= _viewModel.Sarchevi[0].page)
                            {
                                activeChapter = 0;
                                break;
                            }

                            if (_viewModel.CurrentPage >= _viewModel.Sarchevi[^1].page)
                            {
                                activeChapter = _viewModel.Sarchevi.Count - 1;
                                break;
                            }

                            if (_viewModel.CurrentPage >= _viewModel.Sarchevi[i].page)
                            {
                                if (
                                    _viewModel.CurrentPage
                                    < _viewModel
                                        .Sarchevi[Math.Min(i + 1, _viewModel.Sarchevi.Count - 1)]
                                        .page
                                )
                                {
                                    activeChapter = i;
                                    break;
                                }
                            }
                        }
                    }

                    string target = _viewModel.ParsedChapters[targetIndex].Labels[0];
                    string chapterTitle = "თავი";
                    
                    if (_viewModel.ChapterTitles != null && _viewModel.ChapterTitles.Count > 0)
                    {
                        int titleIndex = activeChapter > 0 ? activeChapter : 0;
                        if (titleIndex < _viewModel.ChapterTitles.Count)
                        {
                            chapterTitle = _viewModel.ChapterTitles[titleIndex];
                        }
                    }

                    _resourceManager.OnBookmarkAdded(
                        this,
                        new ResourceManager.BookmarkAddEventArgs(
                            title: _title,
                            page: _viewModel.CurrentPage,
                            chapterTitle: chapterTitle,
                            text: Regex.Replace(
                                target[..Math.Min(30, target.Length)],
                                @"[a-zA-Z<>]",
                                string.Empty
                            )
                        )
                    );
                    btn.Source = "bookmark_add_green.svg";
                }
                else
                {
                    _resourceManager.OnBookmarkDeleted(
                        this,
                        new ResourceManager.BookmarkDeleteEventArgs(
                            title: _title,
                            page: _viewModel.CurrentPage
                        )
                    );
                    btn.Source = "bookmark_add.svg";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bookmark Error: {ex.Message}");
            }
        }

        private async void TapGestureRecognizer_Tapped_1(object sender, TappedEventArgs e)
        {
            try
            {
                //await Shell.Current.Navigation.PushModalAsync(new FontPopup(() => _viewModel.FontSize += 2, () => _viewModel.FontSize -= 2), true);

                FontPopup fp = new FontPopup(
                    () => _viewModel.FontSize += 2,
                    () => _viewModel.FontSize -= 2,
                    (THEMES option) =>
                    {
                        ChangeTheme(option, false);
                        _viewModel.BookManagement.ChangeBookState(
                            new BookStateEventArgs(_viewModel.BookState, theme: option)
                        );
                    },
                    changeAlignment: (alignment) =>
                    {
                        _viewModel.Alignment = alignment;
                    }
                );
                await this.ShowPopupAsync(fp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Font Popup Error: {ex.Message}");
            }
        }

        private async void ImageButton_Clicked_1(object sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                if (_viewModel.Sarchevi == null || _viewModel.Sarchevi.Count == 0)
                {
                    await DisplayAlert("Info", "სარჩევი ჯერ არ არის მზად.", "OK");
                    return;
                }

                int activeChapter = 0;

                for (int i = 0; i < _viewModel.Sarchevi.Count; i++)
                {
                    if (_viewModel.CurrentPage <= _viewModel.Sarchevi[0].page)
                    {
                        activeChapter = 0;
                        break;
                    }

                    if (_viewModel.CurrentPage >= _viewModel.Sarchevi[^1].page)
                    {
                        activeChapter = _viewModel.Sarchevi.Count - 1;
                        break;
                    }

                    if (_viewModel.CurrentPage >= _viewModel.Sarchevi[i].page)
                    {
                        if (
                            _viewModel.CurrentPage
                            < _viewModel.Sarchevi[Math.Min(i + 1, _viewModel.Sarchevi.Count - 1)].page
                        )
                        {
                            activeChapter = i;
                            break;
                        }
                    }
                }

                // Verify resource manager bookmarks exist
                var bookmarks = _resourceManager.GetBookmarks(_title);
                var orderedBookmarks = bookmarks != null 
                    ? [.. bookmarks.OrderBy(x => x.Page)]
                    : new BookmarkedPage[0];

                BookmarksModal bm = new BookmarksModal(
                    _viewModel.Book?.Title ?? "usaxelo",
                    _viewModel.Sarchevi,
                    (int x) =>
                    {
                        Carousel.ScrollTo(x, animate: false);
                        TapGestureRecognizer_Tapped(this, new TappedEventArgs(this));
                    },
                    bookmarks: orderedBookmarks,
                    Math.Max(0, activeChapter)
                );

                await Shell.Current.Navigation.PushModalAsync(bm, true);
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"TOC Error: {ex.Message}");
            }
        }

        private async void TapGestureRecognizer_Tapped_2(object sender, TappedEventArgs e)
        {
            string textarea = ((Label)sender).Text;
            List<string> values = _viewModel
                .Notes.Where(kvp => textarea.Contains($"[{kvp.Key}]"))
                .Select(kvp => kvp.Value)
                .ToList();
            if (values.Count == 0)
            {
                return;
            }

            DictionaryPopup dp = new DictionaryPopup(values);
            await this.ShowPopupAsync(dp);
        }

        private async void FlickSearchVisibility(object sender, EventArgs e)
        {
            //searchGrid.IsVisible = !searchGrid.IsVisible;
            //await Task.Run(() => _viewModel.RevertSearchCommand.Execute(this));
            //b1.IsVisible = !b1.IsVisible;
            //b2.IsVisible = !b2.IsVisible;
            //b3.IsVisible = !b3.IsVisible;
            //b4.IsVisible = !b4.IsVisible;

            SearchWindow.IsVisible = true;
            await SearchWindow.FadeTo(1, 500, Easing.SpringIn);
        }

        private async void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            ModalReaderViewModel.SearchItem item = (ModalReaderViewModel.SearchItem)e.SelectedItem;
            await SearchWindow.FadeTo(0, 500, Easing.SpringOut);
            SearchWindow.IsVisible = false;
            this.ScrollCarousel(item.Page);
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            await SearchWindow.FadeTo(0, 500, Easing.SpringOut);
            SearchWindow.IsVisible = false;
            if (_viewModel.SearchText == "")
            {
                await Task.Run(() => _viewModel.RevertSearchCommand.Execute(this));
            }
        }

        private async void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (e.NewTextValue.Length >= 3)
            {
                await Task.Run(() => _viewModel.SearchCommand.Execute(e.NewTextValue));
            }
        }

        private void TapGestureRecognizer_Tapped_3(object sender, TappedEventArgs e)
        {
            _paused = !_paused;
            PlayerPlayPauseIcon.Source = _paused ? "paused.png" : "playing.png";
            _viewModel.PlayPauseCommand.Execute(this);
        }

        private void AudioSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            _viewModel.AudioCurrentPositionFormated = e.NewValue.ToString();
        }
    }

    public class PageData
    {
        public ObservableCollection<string> Labels { get; set; } = [];
        public ImageSource? Logo { get; set; }
        public string ImgPath { get; set; } = "";
        public bool IsVisible { get; set; } = false;
    }

    public class AudioBookChapters : INotifyPropertyChanged
    {
        public string Text { get; set; } = "";
        public string PathOnDisk { get; set; } = "";
        public string CodeForDownload { get; set; } = "";
        public string Icon { get; set; } = "";

        private string _color = "";

        public string Color
        {
            get => this._color;
            set
            {
                this._color = value;
                OnPropertyChanged();
            }
        }

        public bool NeedDownload { get; set; }

        private bool _isActive = false;

        public bool IsActive
        {
            get => this._isActive;
            set
            {
                this._isActive = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ModalReaderViewModel : INotifyPropertyChanged, IDisposable
    {
        public bool IsPopulatingContent { get; set; } = false;
        public struct SearchItem
        {
            public int Page { get; set; }
            public string Text { get; set; }
            public string Detail { get; set; }
        }

        private readonly ILogger? _logger;
        private static Lib.AudioPlayer? _player;
        public bool IsPageVisible { get; set; } = false; // Visibility lock
        public async Task<bool> VerifyPlayerState()
        {
            // Wait for metadata, but don't hang forever (3s timeout)
            await Task.WhenAny(MetadataTask, Task.Delay(3000));

            if (_player != null) return true;
            
            // If player is null or stopped, try to restore
            System.Diagnostics.Debug.WriteLine("Self-healing: Restoring audio player state on-demand.");
            await RestoreAudioUIState();
            return _player != null;
        }

        private bool _needsReinitialization = false;
        public bool NeedsReinitialization
        {
            get => _needsReinitialization;
            set => _needsReinitialization = value;
        }

        private TaskCompletionSource _metadataReady = new();
        public Task MetadataTask => _metadataReady.Task;

        public record SearchRevertCoords(int PageIndex, int LabelIndex, string TextBefore);

        public event PropertyChangedEventHandler? PropertyChanged;

        private Queue<SearchItem> _pagesWithOccurances = new Queue<SearchItem>();

        private readonly Queue<SearchRevertCoords> _searchRevertCoords =
            new Queue<SearchRevertCoords>();

        private readonly Timer _timer = new Timer
        {
            AutoReset = false,
            Enabled = false,
            Interval = 500,
        };

        // FIX V17.6: Event for Imperative Scrolling
        public event Action<int>? ScrollRequested;

        private readonly Timer _audioUpdateTimer = new Timer
        {
            AutoReset = true,
            Enabled = false,
            Interval = 1000,
        };

        public Queue<SearchItem> PagesWithOccurances
        {
            get => _pagesWithOccurances;
            set
            {
                this._pagesWithOccurances.Clear();
                this._pagesWithOccurances = value;
                OnPropertyChanged();
            }
        }

        public ICommand RevertSearchCommand =>
            new Command(execute: () =>
            {
                while (_searchRevertCoords.Count > 0)
                {
                    SearchRevertCoords src = _searchRevertCoords.Dequeue();
                    ParsedChapters[src.PageIndex].Labels[src.LabelIndex] = src.TextBefore;
                }

                PagesWithOccurances = new Queue<SearchItem>();
                SearchIsEmpty = false;
            });

        public ICommand SearchCommand =>
            new Command<string>(
                execute: (text) =>
                {
                    SearchText = text;
                    _timer.Stop();
                    _timer.Start();

                    //this.RevertSearchCommand.Execute(this);

                    //Queue<SearchItem> temp = new Queue<SearchItem>();
                    //foreach (
                    //    (PageData page, int index) in this.ParsedChapters.Select(
                    //        (val, i) => (val, i)
                    //    )
                    //)
                    //{
                    //    for (int i = 0; i < page.Labels.Count; i++)
                    //    {
                    //        List<string> splitedLabel = page.Labels[i].Split(' ').ToList();
                    //        List<int> matchingIndexes = splitedLabel
                    //            .Select((val, idx) => new { val, idx })
                    //            .Where(item => item.val.Contains(SearchText))
                    //            .Select(item => item.idx)
                    //            .ToList();
                    //        if (matchingIndexes.Count <= 0)
                    //            continue;
                    //        this._searchRevertCoords.Enqueue(
                    //            new SearchRevertCoords(index, i, page.Labels[i])
                    //        );

                    //        foreach (
                    //            string? combined in from idx in matchingIndexes
                    //            let prev2 = idx > 1 ? splitedLabel[idx - 2] : ""
                    //            let prev = idx > 0 ? splitedLabel[idx - 1] : ""
                    //            let next = idx < splitedLabel.Count - 1 ? splitedLabel[idx + 1] : ""
                    //            let next2 = idx < splitedLabel.Count - 2
                    //                ? splitedLabel[idx + 2]
                    //                : ""
                    //            let highlightedMiddle = HighlightMatchingPart(
                    //                RemoveHtmlTags(splitedLabel[idx]),
                    //                SearchText
                    //            )
                    //            select $" {RemoveHtmlTags(prev)} {highlightedMiddle} {RemoveHtmlTags(next)} "
                    //        )
                    //        {
                    //            page.Labels[i] = page.Labels[i]
                    //                .Replace(
                    //                    SearchText,
                    //                    $"<span style=\"background-color: yellow;\">{SearchText}</span>"
                    //                );
                    //            temp.Enqueue(
                    //                new SearchItem
                    //                {
                    //                    Page = index + 1,
                    //                    Text = combined,
                    //                    Detail = $"{index + 1} გვ",
                    //                }
                    //            );
                    //        }
                    //    }
                    //}
                    //PagesWithOccurances = temp;
                    //SearchIsEmpty = temp.Count == 0;

                    //return;

                    //string HighlightMatchingPart(string word, string searchText)
                    //{
                    //    int matchIndex = word.IndexOf(
                    //        searchText,
                    //        StringComparison.OrdinalIgnoreCase
                    //    );

                    //    if (matchIndex == -1)
                    //        return word;

                    //    string beforeMatch = word[..matchIndex];
                    //    string match = word.Substring(matchIndex, searchText.Length);
                    //    string afterMatch = word[(matchIndex + searchText.Length)..];

                    //    return $"{beforeMatch}<b><u><span style=\"color: #9FE870;\">{match}</span></u></b>{afterMatch}";
                    //}

                    //string RemoveHtmlTags(string input) =>
                    //    Regex.Replace(input, "<.*?>", string.Empty);
                }
            );

        public ICommand PlayPauseCommand =>
            new Command(async () =>
            {
                if (!await VerifyPlayerState()) return;

                if (_player.IsPlaying)
                {
                    _player.OnPause();
                    this._audioResourceManager?.OnAudioResourceChanged(
                        this,
                        new ResourceManager.AudioResourceChangedEventArgs(
                            this._bookItem.Title ?? "#",
                            this._ativeAudioFile,
                            this._activeChapterCode,
                            this._audioCurrentPosition
                        )
                    );
                    this._audioResourceManager?.ForceSaveProgress();
                }
                else
                {
                    _player.OnPlay(new Lib.AudioPlayer.PlayEventArgs(AudioCurrentPosition));
                }
            });

        public ICommand SkipForwardCommand =>
            new Command(async () => {
                if (!await VerifyPlayerState()) return;
                _player?.OnSkipForward(seconds: 30);
            });

        public ICommand SkipBackwardCommand =>
            new Command(async () => {
                if (!await VerifyPlayerState()) return;
                _player?.OnSkipBackward(seconds: 30);
            });

        public ICommand RenderAudioChaptersPopupCommand =>
            new Command(async () =>
            {
                // CRITICAL: Wait for metadata to be processed before showing the list
                await MetadataTask;

                if (Application.Current == null || Application.Current.MainPage == null)
                    return;

                if (TotalAudioChapters == null || ChapterNames == null) {
                    LastError = "Err: No chapters list";
                    return;
                }

                // REFRESH: Ensure available chapters are up-to-date before opening the popup
                this.AvailableAudioChapters = this._audioResourceManager?.AvailableMp3Content(this._bookItem.Title ?? "#") ?? [];

                try {
                AudioBookChaptersPopup popup = new AudioBookChaptersPopup(
                    new ObservableCollection<AudioBookChapters>(
                        TotalAudioChapters.Select(
                            (chapter, i) =>
                            {
                                string? matching = AvailableAudioChapters?.FirstOrDefault(ac =>
                                    ac.EndsWith($"{chapter}.mp3")
                                );

                                string chName = (ChapterNames != null && i < ChapterNames.Length) ? ChapterNames[i] : $"თავი {i + 1}";

                                if (matching != null)
                                {
                                    return new AudioBookChapters
                                    {
                                        NeedDownload = false,
                                        Text = $"{i + 1, -5}{chName, 20}",
                                        CodeForDownload = chapter,
                                        PathOnDisk = matching,
                                        Color = "#EDEFEB",
                                        Icon = "delete_chapter.png",
                                    };
                                }
                                else
                                {
                                    return new AudioBookChapters
                                    {
                                        NeedDownload = true,
                                        Text = $"{i + 1, -5}{chName, 20}",
                                        CodeForDownload = chapter,
                                        Icon = "download_chapter.png",
                                        Color = "#949494",
                                    };
                                }
                            }
                        )
                    ),
                    activeChaperIndex: Array.IndexOf(TotalAudioChapters, this._activeChapterCode),
                    downloadAction: (string id) => Task.Run(() => DownloadAudioChapterAsync(id)),
                    deleteAction: (string id) => Task.Run(() => DeleteAudioChapter(id)),
                    changeAudio: (string id) => Task.Run(() => ChangeAudioAsync(id)),
                    downloadAllAction: () => DownloadAllChapterCommand.Execute(this)
                );

                Application.Current.MainPage.ShowPopup(popup);
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"DEBUG_POPUP: Pop-up error: {ex.Message}");
                    this.LastError = "Popup Issue";
                }
            });

        public ICommand SliderDragStartedCommand =>
            new Command(() =>
            {
                _audioUpdateTimer.Stop();
                this._logger?.Out($"[DRAG STARTED] {AudioCurrentPosition}");
            });

        public ICommand SliderDragCompletedCommand =>
            new Command(() =>
            {
                this._logger?.Out($"[DRAG ENDED] {AudioCurrentPosition}");
                _player?.Goto(AudioCurrentPosition);
                this._audioResourceManager?.OnAudioResourceChanged(
                    this,
                    new ResourceManager.AudioResourceChangedEventArgs(
                        this._bookItem.Title ?? "#",
                        this._ativeAudioFile,
                        this._activeChapterCode,
                        this._audioCurrentPosition
                    )
                );
                this._audioResourceManager?.ForceSaveProgress();
                _audioUpdateTimer.Start();
            });

        public ICommand LoadPreviousChapter =>
            new Command(async () =>
            {
                try {
                    int currentChapterIndex = Array.IndexOf(TotalAudioChapters, _activeChapterCode);
                    if (currentChapterIndex > 0)
                    {
                        await ChangeAudioAsync(TotalAudioChapters[currentChapterIndex - 1]);
                        PreviousChapterButtonEnabled = (currentChapterIndex - 1) > 0;
                        NextChapterButtonEnabled = true;
                    }
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"DEBUG_NAVIGATION: LoadPrevious error: {ex.Message}");
                }
            });

        public ICommand LoadNextChapter =>
            new Command(async () =>
            {
                try {
                    int currentChapterIndex = Array.IndexOf(TotalAudioChapters, _activeChapterCode);
                    if (currentChapterIndex != -1 && currentChapterIndex < TotalAudioChapters.Length - 1)
                    {
                        await ChangeAudioAsync(TotalAudioChapters[currentChapterIndex + 1]);
                        NextChapterButtonEnabled = (currentChapterIndex + 1) < TotalAudioChapters.Length - 1;
                        PreviousChapterButtonEnabled = true;
                    }
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"DEBUG_NAVIGATION: LoadNext error: {ex.Message}");
                }
            });

        public ICommand DownloadAllChapterCommand =>
            new Command(() =>
            {
                if (Application.Current == null || Application.Current.MainPage == null)
                    return;

                // 1 chapter ~ 6_897_997 b

                float mbsApproximated =
                    (TotalAudioChapters.Length - AvailableAudioChapters.Length) * 6.9f;

                Application.Current.MainPage.ShowPopup(
                    new DownloadChaptersPopup(() => Task.Run(DownloadAllChapter), mbsApproximated)
                );
            });

        public List<string> ChapterTitles = new List<string>();
        public List<string> CollectedImagePaths { get; set; } = new List<string>();

        public Func<Stream>? LogoStream { get; set; }

        public Queue<Func<Stream>> Images { get; set; } = new Queue<Func<Stream>>();
        public string? FirstPage { get; set; }

        //public ObservableCollection<string> ParsedChapters { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<PageData> ParsedChapters { get; set; } =
            new ObservableCollection<PageData>();

        public Dictionary<int, string> Notes = new Dictionary<int, string>();

        public List<(string chapter, int page)> Sarchevi { get; set; } =
            new List<(string chapter, int page)>();

        // Wrapper property to maintain compatibility while using the shared ResourceManager
        public BookManagement BookManagement => _audioResourceManager as BookManagement ?? Provider.GetService<ResourceManager>() ?? new BookManagement();

        private string _lastError = string.Empty;
        public string LastError
        {
            get => _lastError;
            set
            {
                _lastError = value;
                OnPropertyChanged(nameof(LastError));
            }
        }

        private BookItem _bookItem;
        private BookState _bookState;

        private bool _isBusy = false;

        //private Memory<char> _bookContent;
        private string _bookContent = "";

        private string _searchText = "";

        private double _sreenWidth;
        private double _screenHeight;

        private int _fontSize;
        private int _pages = 0;
        private int _currentPageIndex = 1;
        private bool _searchIsEmpty = false;

        private string[]? _availableAudioChapters;
        private string[]? _totalAudioChapters;
        private string[]? _chapterNames;
        private Func<Stream, Task<byte[]>>? _decryptAudioCallback;

        private bool _previousChapterButtonEnabled = true;
        private bool _nextChapterButtonEnabled = true;
        private double _audioCurrentPosition = 0.0f;
        private double _audioTotalLength = 0.0f;
        private string _audioCurrentPositionFormated = "...";
        private string _audioTotalLengthFormated = "...";
        private string _audioCurrentChapter = "...";
        private string _audioTotalChapterText = "...";

        private string _activeChapterCode = "";
        private string _ativeAudioFile = "";
        private string _playIcon = "playing.png";

        private readonly Dictionary<string, string> _chapterCodeNameMap = new();

        private ResourceManager? _audioResourceManager { get; set; }

        public string[] AvailableAudioChapters
        {
            get => this._availableAudioChapters?.Length > 0 ? this._availableAudioChapters : [];
            set
            {
                this._availableAudioChapters = value;
                OnPropertyChanged();
            }
        }

        public string[] TotalAudioChapters
        {
            get => this._totalAudioChapters?.Length > 0 ? this._totalAudioChapters : [];
            set
            {
                this._totalAudioChapters = value;
                OnPropertyChanged();
            }
        }

        public string[] ChapterNames
        {
            get => this._chapterNames?.Length > 0 ? this._chapterNames : [];
            set
            {
                this._chapterNames = value;
                OnPropertyChanged();
            }
        }

        public string CurrentPageDisplay => @$"{CurrentPage}/{Pages}";

        public bool SearchIsEmpty
        {
            get => this._searchIsEmpty;
            set
            {
                this._searchIsEmpty = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => this._searchText;
            set
            {
                this._searchText = value;
                OnPropertyChanged();
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize == value || value <= 0)
                    return;
                _fontSize = value;

                OnPropertyChanged();
                PopulateContent();
                Task.Run(() =>
                {
                    BookManagement.ChangeBookState(new BookStateEventArgs(BookState, FontSize));
                    /*Guard
                        .AgainstNull(*/Provider.GetService<ResourceManager>()/*)*/
                        .OnBookmarkDeleteAll(
                            this,
                            new ResourceManager.BookmarkDeleteAllEventArgs(
                                title: Book.Title
                            )
                        );
                });
            }
        }

        private TextAlignment _alignment;

        public TextAlignment Alignment
        {
            get => _alignment;
            set
            {
                if (_alignment != value)
                {
                    _alignment = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Pages
        {
            get => _pages;
            set
            {
                if (_pages == value)
                    return;
                _pages = value;
                BookState.TotalPages = Pages;
                OnPropertyChanged();
                Task.Run(() => BookManagement.ChangeBookState(new BookStateEventArgs(BookState)));
            }
        }

        public int CurrentPage
        {
            get => _currentPageIndex > 0 ? _currentPageIndex : 1;
            set
            {
                if (_currentPageIndex == value)
                    return;
                _currentPageIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPageDisplay));
                OnPropertyChanged(nameof(LastVisitedPage));
            }
        }

        public int LastVisitedPage
        {
            get => BookState != null ? BookState.LastVisitedPage - 1 : 0;
            set
            {
                // FIX V17.5: Ignore binding updates ("noise") when repopulating content
                // This prevents the Carousel from resetting our position to 0 during Clear()
                if (IsPopulatingContent) return;

                if (BookState != null && BookState.LastVisitedPage != value + 1)
                {
                    BookState.LastVisitedPage = value + 1;
                    OnPropertyChanged();
                }
            }
        }

        public BookItem Book
        {
            get => _bookItem;
            set
            {
                _bookItem = value;
                OnPropertyChanged();
            }
        }

        public BookState BookState
        {
            get => _bookState;
            set
            {
                _bookState = value;
                BookManagement.ChangeBookState(new BookStateEventArgs(BookState));
            }
        }

        public string AudioCurrentChapter
        {
            get => _audioCurrentChapter;
            set
            {
                _audioCurrentChapter = value;
                OnPropertyChanged();
            }
        }

        public string AudioTotalChapterText
        {
            get => _audioTotalChapterText;
            set
            {
                _audioTotalChapterText = value;
                OnPropertyChanged();
            }
        }

        public string AudioCurrentPositionFormated
        {
            get => _audioCurrentPositionFormated;
            set
            {
                double seconds = double.Parse(value);
                int minutes = (int)(seconds / 60);
                int secs = (int)(seconds % 60);
                _audioCurrentPositionFormated = $"{minutes:D2}:{secs:D2}";
                OnPropertyChanged();
            }
        }

        public string AudioTotalLengthFormated
        {
            get => _audioTotalLengthFormated;
            set
            {
                double seconds = double.Parse(value);
                int minutes = (int)(seconds / 60);
                int secs = (int)(seconds % 60);
                _audioTotalLengthFormated = $"{minutes:D2}:{secs:D2}";
                OnPropertyChanged();
            }
        }

        public double AudioCurrentPosition
        {
            get => _audioCurrentPosition;
            set
            {
                _audioCurrentPosition = value;
                OnPropertyChanged();
            }
        }

        public double AudioTotalLength
        {
            get => _audioTotalLength;
            set
            {
                _audioTotalLength = value;
                OnPropertyChanged();
            }
        }

        public string PlayIcon
        {
            get => _playIcon;
            set
            {
                _playIcon = value;
                OnPropertyChanged();
            }
        }

        public bool PreviousChapterButtonEnabled
        {
            get => _previousChapterButtonEnabled;
            set
            {
                this._previousChapterButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool NextChapterButtonEnabled
        {
            get => _nextChapterButtonEnabled;
            set
            {
                this._nextChapterButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                this._isBusy = value;
                OnPropertyChanged();
            }
        }

        public ModalReaderViewModel(BookItem item, Action<THEMES>? changeCheme = null)
        {
            _bookItem = item;
            // Use the wrapper property which resolves the shared manager
            BookStateManager bsm = this.BookManagement.BookState(_bookItem.Title!);
            _bookState = bsm.Books.FirstOrDefault() ?? new BookState { Title = _bookItem.Title! };

            if (bsm.Theme != null)
            {
                changeCheme?.Invoke((THEMES)bsm.Theme);
            }

            _fontSize = (int)(bsm.FontSize > 0 ? bsm.FontSize : 16);
            _alignment = TextAlignment.Start;

            // Safe service resolution
            this._logger = Provider.GetService<ILogger>();
            if (this._logger == null)
            {
                // Fallback to a basic logger or just swallow if Provider is not ready
                System.Diagnostics.Debug.WriteLine("Warning: ILogger not ready in ModalReaderViewModel constructor.");
            }

            this._timer.Elapsed += SearchTimerElapsed;
            this._audioUpdateTimer.Elapsed += _audioUpdateTimer_Elapsed;

            MessagingCenter.Subscribe<App>(
                this,
                "AppSleep",
                (sender) =>
                {
                    // CRITICAL: Pause the player and save progress immediately on sleep
                    // to prevent "double audio" ghosts and ensure position is captured.
                    if (_player != null && _player.IsPlaying) {
                        _player.OnPause();
                    }
                    _audioResourceManager?.ForceSaveProgress();
                }
            );

            MessagingCenter.Subscribe<App>(
                this,
                "AppResume",
                async (sender) =>
                {
                    // Clean start for return
                    this.NeedsReinitialization = true;
                    // We don't force restore here to avoid race conditions. 
                    // VerifyPlayerState() will handle it on first click or OnAppearing.
                    System.Diagnostics.Debug.WriteLine("AppResume: Marked for lazy restoration.");
                }
            );
        }

        public void ReattachGlobalPlayer()
        {
            if (_player != null)
            {
                _player.PlaybackEnded -= _player_PlaybackEnded;
                _player.PlaybackEnded += _player_PlaybackEnded;
                _player.AudioStateEvent -= OnPlayerStateChanged;
                _player.AudioStateEvent += OnPlayerStateChanged;
                // Update icon to match current player state
                this.PlayIcon = _player.IsPlaying ? "playing" : "paused";
            }
        }

        private void OnPlayerStateChanged(object? sender, Lib.AudioPlayer.AudioStateEventArgs args) =>
            this.PlayIcon = args.IsPLaying ? "playing" : "paused";

        public async Task RestoreAudioUIState()
        {
            try {
                // Ensure manager is resolved (self-healing)
                if (this._audioResourceManager == null)
                {
                    this._audioResourceManager = Provider.GetService<ResourceManager>();
                }
                
                if (this._bookItem == null || this._audioResourceManager == null) return;

                // CRITICAL: Wait for the manager to finish loading from disk
                await this._audioResourceManager.InitializationTask;

                var state = this._audioResourceManager.ContinueFrom(this._bookItem.Title ?? "");
                if (state != null) {
                    this._ativeAudioFile = state.Value.audioFile;
                    this._activeChapterCode = state.Value.chapterCode;
                    
                    if (!string.IsNullOrEmpty(this._activeChapterCode) && _chapterCodeNameMap.TryGetValue(this._activeChapterCode, out string? chapterName)) {
                        AudioCurrentChapter = chapterName;
                    }

                    AudioCurrentPosition = state.Value.position;
                    // Format directly to avoid Parse issues
                    int minutes = (int)(AudioCurrentPosition / 60);
                    int secs = (int)(AudioCurrentPosition % 60);
                    _audioCurrentPositionFormated = $"{minutes:D2}:{secs:D2}";
                    
                    // Trigger property changes to refresh UI labels
                    OnPropertyChanged(nameof(AudioCurrentChapter));
                    OnPropertyChanged(nameof(AudioCurrentPosition));
                    
                    // Force timer restart to keep UI moving
                    _audioUpdateTimer.Enabled = true;

                    // CRITICAL: Restore the physical player if it was killed by OS
                    if (_player == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Self-healing: Physical player is missing. Re-initializing.");
                        ReinitializeAudioPlayer();
                    }

                    System.Diagnostics.Debug.WriteLine($"Restored UI State: {AudioCurrentChapter} at {AudioCurrentPositionFormated} (Timer Enabled)");
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error restoring UI state: {ex.Message}");
            }
        }

        public void ForceSaveAudioProgress()
        {
            _audioResourceManager?.ForceSaveProgress();
        }

        private void _audioUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_player == null)
                return;

            AudioCurrentPosition = _player.GetCurrentPosition();
            AudioTotalLength = _player.GetTotalLength();

            AudioCurrentPositionFormated = this.AudioCurrentPosition.ToString();
            AudioTotalLengthFormated = this.AudioTotalLength.ToString();

            _audioResourceManager?.OnAudioResourceChanged(
                this,
                new ResourceManager.AudioResourceChangedEventArgs(
                    this._bookItem.Title ?? "#",
                    this._ativeAudioFile,
                    this._activeChapterCode,
                    this._audioCurrentPosition
                )
            );
        }

        private void SearchTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                this.RevertSearchCommand.Execute(this);

                Queue<SearchItem> temp = new Queue<SearchItem>();
                foreach (
                    (PageData page, int index) in this.ParsedChapters.Select((val, i) => (val, i))
                )
                {
                    for (int i = 0; i < page.Labels.Count; i++)
                    {
                        List<string> splitedLabel = page.Labels[i].Split(' ').ToList();
                        List<int> matchingIndexes = splitedLabel
                            .Select((val, idx) => new { val, idx })
                            .Where(item => item.val.Contains(SearchText))
                            .Select(item => item.idx)
                            .ToList();
                        if (matchingIndexes.Count <= 0)
                            continue;
                        this._searchRevertCoords.Enqueue(
                            new SearchRevertCoords(index, i, page.Labels[i])
                        );

                        foreach (
                            string? combined in from idx in matchingIndexes
                            let prev2 = idx > 1 ? splitedLabel[idx - 2] : ""
                            let prev = idx > 0 ? splitedLabel[idx - 1] : ""
                            let next = idx < splitedLabel.Count - 1 ? splitedLabel[idx + 1] : ""
                            let next2 = idx < splitedLabel.Count - 2 ? splitedLabel[idx + 2] : ""
                            let highlightedMiddle = HighlightMatchingPart(
                                RemoveHtmlTags(splitedLabel[idx]),
                                SearchText
                            )
                            select $"{RemoveHtmlTags(prev)} {highlightedMiddle} {RemoveHtmlTags(next)} "
                        )
                        {
                            page.Labels[i] = page.Labels[i]
                                .Replace(
                                    SearchText,
                                    $"<span style=\"background-color: yellow;\">{SearchText}</span>"
                                );

                            System.Diagnostics.Debug.WriteLine(combined);

                            temp.Enqueue(
                                new SearchItem
                                {
                                    Page = index + 1,
                                    Text = combined.Replace("<h1", string.Empty),
                                    Detail = $"{index + 1} გვ",
                                }
                            );
                        }
                    }
                }

                PagesWithOccurances = temp;
                SearchIsEmpty = temp.Count == 0;
                return;

                string HighlightMatchingPart(string word, string searchText)
                {
                    int matchIndex = word.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);

                    if (matchIndex == -1)
                        return word;

                    string beforeMatch = word[..matchIndex];
                    string match = word.Substring(matchIndex, searchText.Length);
                    string afterMatch = word[(matchIndex + searchText.Length)..];

                    return $"{beforeMatch}<b><u><span style=\"color: #9FE870;\">{match}</span></u></b>{afterMatch}";
                }

                string RemoveHtmlTags(string input) => Regex.Replace(input, ".*?>", string.Empty);
            }
            catch (Exception ex)
            {
                this._logger?.Out(ex);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void InitializeContent(string content)
        {
            //_bookContent = new Memory<char>(content.ToCharArray());
            _bookContent = content;
        }

        public void InitializeDimensions(double width, double height)
        {
            _sreenWidth = width;
            _screenHeight = height;

            Task.Run(() => PopulateContent());
        }

        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            // Remove HTML tags
            input = Regex.Replace(input, "<.*?>", string.Empty);
            // Keep only letters and digits
            char[] arr = input.Where(c => char.IsLetterOrDigit(c)).ToArray();
            return new string(arr).ToLowerInvariant();
        }

        private Task PopulateSarchevi(List<PageData> sourcePages)
        {
            // V17.2: Fix Crash - Modify Collections on Main Thread
            MainThread.BeginInvokeOnMainThread(() => {
                Sarchevi.Clear();
            });
            
            var newSarcheviItems = new List<(string, int)>();

            // Pre-normalize chapter titles for performance
            var normalizedTitles = ChapterTitles.Select(t => new { Original = t, Normalized = NormalizeString(t) }).ToList();

            int chapterIndex = 0;
            // Scan all pages
            for (int pageIdx = 0; pageIdx < sourcePages.Count; pageIdx++)
            {
                if (chapterIndex >= normalizedTitles.Count) break;

                var pageData = sourcePages[pageIdx];
                if (pageData?.Labels == null || pageData.Labels.Count == 0) continue;

                // Check first 3 lines of the page
                int linesToCheck = Math.Min(3, pageData.Labels.Count);
                for (int lineIdx = 0; lineIdx < linesToCheck; lineIdx++)
                {
                    string lineText = pageData.Labels[lineIdx];
                    if (string.IsNullOrWhiteSpace(lineText)) continue;

                    string normalizedLine = NormalizeString(lineText);
                    string target = normalizedTitles[chapterIndex].Normalized;

                    // FUZZY MATCH LOGIC
                    // 1. Exact match of normalized strings
                    // 2. Line starts with Title (e.g. "Chapter 1 \n The Beginning")
                    // 3. Title contains Line (rare, but for weird splitting)
                    
                    // Only match if lengths are reasonable to avoid matching "I" to everything
                    if (normalizedLine.Length < 2 && target.Length < 2)
                    {
                        if (normalizedLine == target)
                        {
                            newSarcheviItems.Add((normalizedTitles[chapterIndex].Original, pageIdx + 1));
                            chapterIndex++;
                            break; // Found chapter on this page, move to next chapter
                        }
                    }
                    else if (normalizedLine.StartsWith(target) || (target.Length > 4 && normalizedLine.Contains(target)))
                    {
                         newSarcheviItems.Add((normalizedTitles[chapterIndex].Original, pageIdx + 1));
                         chapterIndex++;
                         break; // Found chapter on this page
                    }
                }
            }

            // Update UI bound collection on Main Thread
            if (newSarcheviItems.Count > 0)
            {
                 MainThread.BeginInvokeOnMainThread(() => {
                     foreach(var item in newSarcheviItems)
                     {
                         Sarchevi.Add(item);
                     }
                 });
            }
            return Task.CompletedTask;
        }

        public void PopulateContent(bool landscape = false)
        {
            // V17: SAFE RE-ENTRANCY PROTECTION
            IsPopulatingContent = true;
            int oldPage = CurrentPage;

            string[] files = Directory.GetFiles(FileSystem.AppDataDirectory);
            char[] bullshitChars = ['<', '/', '>', 'b', 'i'];
            if (!(_sreenWidth > 0) || !(_screenHeight > 0))
            {
                IsPopulatingContent = false;
                return;
            }

            // V17.2: Fix Crash - Buffer pages locally to avoid touching UI collection from BG thread
            var newPages = new List<PageData>();
            
            //ParsedChapters.Clear(); // Can't do this here! Moved to end on MainThread.

            // V17.1: OPTIMIZED HEURISTICS
            // Reduced multiplier to 1.4 for tighter, more standard spacing
            double lineHeight = _fontSize * 1.4;
            double avgCharWidth = _fontSize * 0.55;
            int linesPerPage = (int)((_screenHeight - 60) / lineHeight);
            
            // V17.4: Guard against infinite loop if font is too big
            if (linesPerPage < 1) linesPerPage = 1;
            int charsPerLine = (int)((_sreenWidth - 40) / avgCharWidth);

            //StringBuilder page = new ();

            //int charsPerPage = linesPerPage * charsPerLine;
            //int totalLength = _bookContent.Length;
            //int chunkCount = (int)Math.Ceiling((double)totalLength / charsPerPage);

            //for (int i = 0; i < chunkCount; i++)
            //{
            //    int start = i * charsPerPage;
            //    int length = Math.Min(charsPerPage, totalLength - start);
            //    string chunk = new string(_bookContent.Slice(start, length).ToArray());
            //    ParsedChapters.Add(chunk);
            //}

            int imgCounter = 0;

            int charCounter = 0;
            int lineCounter = 0;
            bool isFirst = true;
            bool didTitle = false;
            StringBuilder sb = new StringBuilder();

            if (landscape)
            {
                linesPerPage -= 10;
            }

            if (FirstPage != null)
            {
            if (FirstPage != null)
            {
                //ParsedChapters.Add(
                newPages.Add(
                    new PageData
                    {
                        Logo = ImageSource.FromStream(() => LogoStream?.Invoke()),
                        Labels = new ObservableCollection<string>() { FirstPage },
                        IsVisible = true,
                    }
                );
            }
            }

            for (int i = 0; i < _bookContent.Length; i++)
            {
                if (_bookContent[i] == '[' && _bookContent[i + 1] == '|')
                {
                    i++;
                    newPages.Add(new PageData());

                    if (!isFirst)
                    {
                        charCounter = 0;
                        lineCounter = 0;
                        didTitle = false;
                        //ParsedChapters[^1].Labels.Add(sb.ToString());
                        sb = new StringBuilder();
                    }
                    else
                    {
                        isFirst = false;
                    }

                    sb.Append("<h3>");
                    continue;
                }

                if (_bookContent[i] == '#' && _bookContent[i + 1] == '%')
                {
                    if (imgCounter < CollectedImagePaths.Count)
                    {
                        if (newPages.Count > 0)
                        {
                             newPages[^1].ImgPath = CollectedImagePaths[imgCounter++];
                             newPages[^1].IsVisible = true;
                        }
                        else
                        {
                            imgCounter++; // Skip if no page
                        }
                    }
                    i++;

                    charCounter = 0;
                    lineCounter = 0;

                    if (newPages.Count > 0)
                    {
                        newPages[^1].Labels.Add(sb.ToString());
                        newPages.Add(new PageData());
                    }
                    sb = new StringBuilder();

                    continue;
                }

                if (
                    _bookContent[i] == '<'
                    && _bookContent[i + 1] == 'b'
                    && _bookContent[i + 2] == 'r'
                )
                {
                    i += 3;
                    sb.Append("<br/>");
                    lineCounter++;
                    continue;
                }

                if (_bookContent[i] == '$')
                {
                    sb.Append("<br/>");
                    if (!didTitle)
                    {
                        sb.Append("</h3>");
                        didTitle = true;
                    }

                    charCounter = 0;
                    lineCounter++;
                    continue;
                }

                if (_bookContent[i] == '[' && int.TryParse(_bookContent[i + 1].ToString(), out _))
                {
                    sb.Append("<b>");
                }

                sb.Append(_bookContent[i]);

                if (!bullshitChars.Any(x => x == _bookContent[i]))
                    charCounter++;

                if (_bookContent[i] == ']')
                {
                    sb.Append("</b>");
                }

                if (charCounter >= charsPerLine)
                {
                    lineCounter++;
                    charCounter = 0;
                }

                if (lineCounter < linesPerPage)
                    continue;

                i++;

                while (_bookContent[i] != ' ' && _bookContent[i] != '$')
                {
                    sb.Append(_bookContent[i++]);
                }

                //while (charCounter < charsPerLine)
                //{
                //    if (!bullshitChars.Any(x => x == _bookContent[i]))
                //    {
                //        sb.Append(_bookContent[i]);
                //        charCounter++;
                //        i++;
                //    }
                //}

                //sb.Append('-');

                if (newPages.Count > 0) newPages[^1].Labels.Add(sb.ToString());
                newPages.Add(new PageData());
                sb = new StringBuilder();
                charCounter = 0;
                lineCounter = 0;
            }

            // ADDED: Ensure the very last chunk of text is added to the last page!
            if (sb.Length > 0 && newPages.Count > 0)
            {
                newPages[^1].Labels.Add(sb.ToString());
            }

            // CLEANUP: Remove any empty pages (no images and no meaningful text)
            newPages = newPages.Where(p => 
                p.IsVisible || // Has image
                p.Labels.Any(l => !string.IsNullOrWhiteSpace(l)) // Has text
            ).ToList();

            // Ensure we have at least one page if the book is totally empty (fallback)
            if (newPages.Count == 0) newPages.Add(new PageData());

            // V17.2: CRITICAL - Update UI Collection on Main Thread
            // V17.3: DELAYED RESTORATION - Wait for UI to settle before restoring position
            MainThread.BeginInvokeOnMainThread(async () => {
                ParsedChapters.Clear();
                foreach (var page in newPages)
                {
                    ParsedChapters.Add(page);
                }
                
                // Update Page Count after UI update
                Pages = ParsedChapters.Count;

                // V17.3: Delay to allow CarouselView to digest new items
                await Task.Delay(100);
                
                // V17.4: Restore relative position safely
                if (oldPage > 0)
                {
                    int targetIndex = Math.Min(oldPage - 1, Pages - 1);
                    if (targetIndex < 0) targetIndex = 0;
                    
                    // FIX V17.6: Imperative Scroll Command
                    // Bypasses binding/property changed mechanisms which are unreliable during layout thrashing
                    BookState.LastVisitedPage = targetIndex + 1;
                    this.CurrentPage = targetIndex + 1;
                    
                    // Fire the event to force the View to scroll
                    ScrollRequested?.Invoke(targetIndex);
                    
                    // V17.1: XAML Binding on LastVisitedPage handles the Carousel scroll.
                }

                IsPopulatingContent = false;
                OnPropertyChanged(nameof(CurrentPageDisplay));
            });
            
            Task.Run(() => PopulateSarchevi(newPages));
        }

        public void CreateDictionary(string html)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            HtmlNodeCollection paragraphs = doc.DocumentNode.SelectNodes("//p");
            if (paragraphs == null)
                return;
            foreach (HtmlNode? paragraph in paragraphs)
            {
                HtmlNode supNode = paragraph.SelectSingleNode(".//sup");
                if (supNode == null)
                    continue;
                string keyString = supNode.InnerText;
                if (!int.TryParse(keyString.Trim('[', ']'), out int key))
                    continue;
                string val = paragraph.InnerText;
                val = val.Replace(supNode.OuterHtml, "").Trim();
                Notes[key] = val;
            }
        }

        public void Dispose()
        {
            _audioResourceManager?.ForceSaveProgress();
            string[] files = Directory.GetFiles(FileSystem.AppDataDirectory);
            foreach (string file in files)
            {
                if (
                    Path.GetFileName(file)
                        .Contains("img", StringComparison.CurrentCultureIgnoreCase)
                )
                {
                    File.Delete(file);
                }
            }

            this._audioUpdateTimer.Enabled = false;
            // Definitive disposal of native audio resources ONLY if this instance is closing
            // We use a safe check here.
            try {
                if (_player != null) {
                    _player.PlaybackEnded -= _player_PlaybackEnded;
                    _player.AudioStateEvent -= OnPlayerStateChanged;
                    _player.Dispose();
                }
            } catch { }
            _player = null;

            MessagingCenter.Unsubscribe<App>(this, "AppSleep");
            MessagingCenter.Unsubscribe<App>(this, "AppResume");
        }

        private void ReinitializeAudioPlayer()
        {
            try
            {
                if (string.IsNullOrEmpty(_ativeAudioFile) || !File.Exists(_ativeAudioFile))
                {
                    if (string.IsNullOrEmpty(_ativeAudioFile)) return;
                    string fName = Path.GetFileName(_ativeAudioFile);
                    string? realPath = AvailableAudioChapters?.FirstOrDefault(p => Path.GetFileName(p) == fName);
                    if (realPath != null && File.Exists(realPath))
                    {
                        this._ativeAudioFile = realPath;
                    }
                    else
                    {
                        return;
                    }
                }

                byte[] bytes = File.ReadAllBytes(_ativeAudioFile);
                
                // If we have a decryption callback, we should ideally use it, 
                // but since the file on disk (saved via SaveMp3Async) is already decrypted,
                // we can just read it directly as we do in InitializeAudioPlayer.
                
                _player?.Dispose();
                _player = new Lib.AudioPlayer(bytes);
                _player.PlaybackEnded += _player_PlaybackEnded;
                _player.AudioStateEvent += (sender, args) =>
                    this.PlayIcon = args.IsPLaying ? "playing" : "paused";
                
                if (AudioCurrentPosition > 0)
                {
                    _player.Goto(AudioCurrentPosition);
                }

                // Ensure timer is running
                this._audioUpdateTimer.Enabled = true;
                
                this._logger?.Out("Audio player re-initialized after backgrounding.");
            }
            catch (Exception ex)
            {
                this._logger?.Out($"Failed to re-initialize audio player: {ex.Message}");
            }
        }

        public void InitializeAudioPlayer(
            string[] availableChapters,
            string[] chaptersInTotal,
            string[] chapterNames,
            byte[]? bytes = null,
            Func<Stream, Task<byte[]>>? decryptAudioCallback = null
        )
        {
            this._audioResourceManager = Provider.GetService<ResourceManager>();
            this._audioUpdateTimer.Enabled = true;
            this._decryptAudioCallback = decryptAudioCallback;

            this.AvailableAudioChapters = availableChapters;
            this.TotalAudioChapters = chaptersInTotal;
            this.ChapterNames = chapterNames;

            for (int i = 0; i < chaptersInTotal.Length; i++)
            {
                _chapterCodeNameMap[chaptersInTotal[i]] = chapterNames[i];
            }

            // SIGNAL that metadata is ready for UI consumption
            _metadataReady.TrySetResult();

            try 
            {
                (string audioFile, string chapterCode, double position)? continueFrom =
                    this._audioResourceManager?.ContinueFrom(this._bookItem?.Title ?? "");

                if (continueFrom != null)
                {
                    this._activeChapterCode = continueFrom.Value.chapterCode;
                    this._ativeAudioFile = continueFrom.Value.audioFile;

                    if (_chapterCodeNameMap.TryGetValue(continueFrom.Value.chapterCode, out string? cName)) {
                        AudioCurrentChapter = cName;
                    }

                    AudioTotalChapterText =
                        $"{Array.IndexOf(chaptersInTotal, continueFrom.Value.chapterCode) + 1}/{chaptersInTotal.Length} თავი";
                    
                    // Immediately set initial position and format for the UI
                    AudioCurrentPosition = continueFrom.Value.position;
                    int mins = (int)(AudioCurrentPosition / 60);
                    int scs = (int)(AudioCurrentPosition % 60);
                    _audioCurrentPositionFormated = $"{mins:D2}:{scs:D2}";

                    if (File.Exists(continueFrom.Value.audioFile)) {
                        bytes = File.ReadAllBytes(continueFrom.Value.audioFile);
                    }
                    else
                    {
                        // Path might be stale due to iOS simulator directory rotation
                        string fName = Path.GetFileName(continueFrom.Value.audioFile);
                        string? realPath = availableChapters?.FirstOrDefault(p => Path.GetFileName(p) == fName);
                        if (realPath != null && File.Exists(realPath))
                        {
                            this._ativeAudioFile = realPath;
                            bytes = File.ReadAllBytes(realPath);
                        }
                    }
                }
                else
                {
                    if (availableChapters != null && availableChapters.Length > 0)
                    {
                        bytes ??= File.ReadAllBytes(availableChapters[0]);

                        this._activeChapterCode = chaptersInTotal[0];
                        this._ativeAudioFile = availableChapters[0];
                        
                        if (_chapterCodeNameMap.TryGetValue(this._activeChapterCode, out string? cName)) {
                            AudioCurrentChapter = cName;
                        }

                        AudioTotalChapterText =
                            $"{Array.IndexOf(chaptersInTotal, this._activeChapterCode) + 1}/{chaptersInTotal.Length} თავი";

                        _audioResourceManager?.OnAudioResourceChanged(
                            this,
                            new ResourceManager.AudioResourceChangedEventArgs(
                                this._bookItem?.Title ?? "#",
                                this._ativeAudioFile,
                                this._activeChapterCode,
                                0f
                            )
                        );
                    }
                }
            } 
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Safe UI restoration failed: {ex.Message}");
                // Fallback: at least try to get bytes if possible
                if (bytes == null && availableChapters != null && availableChapters.Length > 0) {
                    try { bytes = File.ReadAllBytes(availableChapters[0]); } catch { }
                }
            }

            if (bytes != null)
            {
                // Only create new player if one doesn't exist OR it's dead
                if (_player == null) {
                    _player = new Lib.AudioPlayer(bytes);
                    _player.PlaybackEnded += _player_PlaybackEnded;
                    _player.AudioStateEvent += OnPlayerStateChanged;
                } else {
                    // Just update content if it's different? 
                    // To be safe, re-attachment is usually enough.
                    this.ReattachGlobalPlayer();
                }
                
                // Securely seek to the saved position without necessarily starting playback
                if (AudioCurrentPosition > 0)
                {
                    _player.Goto(AudioCurrentPosition);
                }
                
                this._logger?.Out($"Audio player initialized at {AudioCurrentPosition}s");
            }
        }



        public void SignalMetadataReady()
        {
            _metadataReady.TrySetResult();
        }

        private void _player_PlaybackEnded(object? sender, EventArgs e)
        {
            // NEW: Always reset position logic might be triggering too early
            MainThread.BeginInvokeOnMainThread(() => {
                if (TotalAudioChapters == null || TotalAudioChapters.Length == 0) {
                    LastError = "Ended: No chapters list";
                    return;
                }

                int currentChapterIndex = Array.IndexOf(TotalAudioChapters, _activeChapterCode);
                if (currentChapterIndex == -1) {
                    LastError = $"Ended: Ch {_activeChapterCode} not found";
                    return;
                }

                // If we are very close to the end, it's a real end
                double duration = _player.GetTotalLength();
                double currentPos = _player.GetCurrentPosition();
                bool isRealEnd = (duration - currentPos) < 2.5; // Slightly more generous buffer
                System.Diagnostics.Debug.WriteLine($"DEBUG_ENDED: PlaybackEnded. Pos: {currentPos}/{duration}. RealEnd: {isRealEnd}");

                if (!isRealEnd && currentPos > 0)
                {
                    LastError = "Ended: Interrupted?";
                    return;
                }

                if (currentChapterIndex < TotalAudioChapters.Length - 1)
                {
                    LastError = "Ended: Going to next...";
                    LoadNextChapter.Execute(null);
                }
                else
                {
                    LastError = "Ended: Last chapter reached";
                    _player.Stop();
                    UpdateAudioInteface(
                        TotalAudioChapters[0],
                        null,
                        0,
                        _chapterCodeNameMap[TotalAudioChapters[0]]
                    );
                }
            });
        }

        private async Task DownloadAudioChapterAsync(string chapterId)
        {
            try
            {
                SessionManagement sessionManagement = new SessionManagement();
                SessionManagement.SessionModel currentUser =
                    await sessionManagement.ReadSessionAsync() ?? throw new Exception();

                KeyManagement keyManagement = new KeyManagement();

                KeyManagement.KeyNode keyNode =
                    (await keyManagement.KeysAsync()).FirstOrDefault(kn =>
                        kn.Username == currentUser.Username && kn.Password == currentUser.Password
                    ) ?? throw new Exception();
                Http http = new Http();

                Stream bookStream = await http.Downloadbook(
                    currentUser.Session,
                    keyNode.DeviceId,
                    chapterId
                );
                bookStream.Seek(0, SeekOrigin.Begin);

                byte[] cleanAudio = await _decryptAudioCallback?.Invoke(bookStream)!;

                _player?.SetAudioContent(cleanAudio, new Lib.AudioPlayer.PlayEventArgs(0));

                string savedPath =
                    await BookManagement.SaveMp3Async(_bookItem.Title ?? "", cleanAudio, chapterId)
                    ?? throw new Exception();

                AvailableAudioChapters = [.. AvailableAudioChapters, savedPath];

                UpdateAudioInteface(chapterId, savedPath, 0f, _chapterCodeNameMap[chapterId]);
                LastError = string.Empty; // Success!
            }
            catch (Exception e)
            {
                LastError = $"Download Error: {e.Message}";
                System.Diagnostics.Debug.WriteLine($"DEBUG_DOWNLOAD ERROR: {e}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SetupDemo(string[] chaptersInTotal, string[] chapterNames)
        {
            this._audioResourceManager = Provider.GetService<ResourceManager>();
            this._audioUpdateTimer.Enabled = true;

            this.TotalAudioChapters = chaptersInTotal;
            this.ChapterNames = chapterNames;

            // Generate silent WAV
            byte[] bytes = GetSilentWavBytes();

            (string audioFile, string chapterCode, double position)? continueFrom =
                this._audioResourceManager?.ContinueFrom(this._bookItem.Title ?? "");

            if (continueFrom != null)
            {
                this._activeChapterCode = continueFrom.Value.chapterCode;
                this._ativeAudioFile = continueFrom.Value.audioFile; // Fake path
                AudioCurrentChapter = chapterNames[0]; // Simplified
                AudioTotalChapterText = "1/2 თავი";
            }
            else
            {
                this._activeChapterCode = chaptersInTotal[0];
                this._ativeAudioFile = "demo_ch_1.wav";
                AudioCurrentChapter = chapterNames[0];
                AudioTotalChapterText = "1/2 თავი";

                _audioResourceManager?.OnAudioResourceChanged(
                    this,
                    new ResourceManager.AudioResourceChangedEventArgs(
                        this._bookItem.Title ?? "#",
                        this._ativeAudioFile,
                        this._activeChapterCode,
                        0f
                    )
                );
            }

            _player = new Lib.AudioPlayer(bytes);
            _player.PlaybackEnded += _player_PlaybackEnded;
            _player.AudioStateEvent += (sender, args) =>
                this.PlayIcon = args.IsPLaying ? "playing" : "paused";
            
            _player.OnPlay(new Lib.AudioPlayer.PlayEventArgs(continueFrom?.position ?? 0));
            SignalMetadataReady();
        }

        private byte[] GetSilentWavBytes()
        {
            int sampleRate = 44100;
            short bitsPerSample = 16;
            short channels = 1;
            int durationSeconds = 5;
            int dataSize = sampleRate * channels * (bitsPerSample / 8) * durationSeconds;
            int fileSize = 36 + dataSize;

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    // RIFF header
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(fileSize);
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                    // fmt subchunk
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16); // Subchunk1Size
                    writer.Write((short)1); // AudioFormat (PCM)
                    writer.Write(channels);
                    writer.Write(sampleRate);
                    writer.Write(sampleRate * channels * (bitsPerSample / 8)); // ByteRate
                    writer.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
                    writer.Write(bitsPerSample);

                    // data subchunk
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(dataSize);

                    // Silent data
                    for (int i = 0; i < dataSize; i++)
                    {
                        writer.Write((byte)0);
                    }
                }
                return ms.ToArray();
            }


        }

        private void DeleteAudioChapter(string chapterId)
        {
            if (this._audioResourceManager == null)
                return;

            if (chapterId == _activeChapterCode)
                return;

            AvailableAudioChapters = AvailableAudioChapters
                .Where(x => !x.Contains(chapterId))
                .ToArray();

            this._audioResourceManager.OnAudioResourceDeleted(
                this,
                new ResourceManager.AudioResourceDeletedEventArgs(Book.Title ?? "#", chapterId)
            );
        }

        private async Task ChangeAudioAsync(string chapterId)
        {
            LastError = $"Loading ch {chapterId}...";
            System.Diagnostics.Debug.WriteLine($"DEBUG_AUDIO: ChangeAudioAsync called for {chapterId}");
            if (this._audioResourceManager == null || _player == null) {
                LastError = "Error: ResourceManager/Player null";
                System.Diagnostics.Debug.WriteLine("DEBUG_AUDIO: ResourceManager or Player is null");
                return;
            }

            (string file, double position)? progress = this._audioResourceManager.ChapterProgress(
                this._bookItem.Title ?? "#",
                chapterId
            );

            string? activeFile = null;
            double position = 0;

            if (progress.HasValue)
            {
                activeFile = progress.Value.file;
                position = progress.Value.position;
                System.Diagnostics.Debug.WriteLine($"DEBUG_AUDIO: Progress found: {activeFile} at {position}s");
            } else {
                System.Diagnostics.Debug.WriteLine("DEBUG_AUDIO: No progress record found for this chapter.");
            }

            activeFile = GetLocalAudioPath(chapterId, activeFile);

            if (string.IsNullOrEmpty(activeFile) || !File.Exists(activeFile)) {
                LastError = $"File not found. Downloading {chapterId}...";
                System.Diagnostics.Debug.WriteLine($"DEBUG_AUDIO: File not found for {chapterId}, triggering download.");
                IsBusy = true;
                _ = Task.Run(() => DownloadAudioChapterAsync(chapterId));
                return;
            }

            System.Diagnostics.Debug.WriteLine($"DEBUG_AUDIO: Resolved path: {activeFile}");
            try {
                byte[] bytes = await File.ReadAllBytesAsync(activeFile);
                _player.SetAudioContent(
                    bytes,
                    new Lib.AudioPlayer.PlayEventArgs(position)
                );
                LastError = string.Empty; // Success
            } catch (Exception ex) {
                LastError = $"Read Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"DEBUG_AUDIO: Error reading or setting audio: {ex.Message}");
            }

            UpdateAudioInteface(
                chapterId,
                activeFile,
                position,
                _chapterCodeNameMap[chapterId]
            );
        }

        private string? GetLocalAudioPath(string chapterId, string? savedPath = null)
        {
            System.Diagnostics.Debug.WriteLine($"DEBUG_PATH: Resolving path for {chapterId}. SavedPath: {savedPath ?? "null"}");
            // 1. If we have a saved path and it exists, use it.
            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                System.Diagnostics.Debug.WriteLine("DEBUG_PATH: Using existing saved path.");
                return savedPath;
            }

            // 2. Fallback: Search in available chapters by filename (handles simulator rotation)
            // Ensure list is fresh if it looks empty or incomplete
            if (AvailableAudioChapters == null || AvailableAudioChapters.Length == 0)
            {
                AvailableAudioChapters = Directory.GetFiles(FileSystem.AppDataDirectory, "*.mp3");
            }

            string? foundInList = AvailableAudioChapters?.FirstOrDefault(ac => ac.EndsWith($"{chapterId}.mp3"));
            if (!string.IsNullOrEmpty(foundInList) && File.Exists(foundInList))
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG_PATH: Resolved via AvailableAudioChapters: {foundInList}");
                return foundInList;
            }

            // 3. Last ditch search directly in filesystem
            string fallbackSearch = Path.Combine(FileSystem.AppDataDirectory, $"{chapterId}.mp3");
            if (File.Exists(fallbackSearch)) {
                 System.Diagnostics.Debug.WriteLine($"DEBUG_PATH: Resolved via Direct Search: {fallbackSearch}");
                 return fallbackSearch;
            }

            System.Diagnostics.Debug.WriteLine("DEBUG_PATH: Path resolution failed.");
            return null;
        }

        private void UpdateAudioInteface(
            string activeChapterCode,
            string activeAudioFile,
            double currentPosition,
            string currentChapter
        )
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                this._activeChapterCode = activeChapterCode;
                this._ativeAudioFile = activeAudioFile;
                this.AudioCurrentPosition = currentPosition;
                this.AudioCurrentChapter = currentChapter;
                this.LastError = string.Empty; // Clear error on interface update
                AudioTotalChapterText =
                    $"{Array.IndexOf(TotalAudioChapters, this._activeChapterCode) + 1}/{TotalAudioChapters.Length} თავი";

                this._audioResourceManager?.OnAudioResourceChanged(
                    this,
                    new ResourceManager.AudioResourceChangedEventArgs(
                        this._bookItem?.Title ?? "#",
                        this._ativeAudioFile,
                        this._activeChapterCode,
                        this.AudioCurrentPosition
                    )
                );
            });
        }

        private async Task DownloadAllChapter()
        {
            try
            {
                IsBusy = true;

                string[] diff = TotalAudioChapters
                    .Where(tac => !AvailableAudioChapters.Any(aac => aac.Contains(tac)))
                    .ToArray();

                SessionManagement sessionManagement = new SessionManagement();
                SessionManagement.SessionModel currentUser =
                    await sessionManagement.ReadSessionAsync() ?? throw new Exception();

                KeyManagement keyManagement = new KeyManagement();

                KeyManagement.KeyNode keyNode =
                    (await keyManagement.KeysAsync()).FirstOrDefault(kn =>
                        kn.Username == currentUser.Username && kn.Password == currentUser.Password
                    ) ?? throw new Exception();
                Http http = new Http();

                foreach (string chapterId in diff)
                {
                    Stream bookStream = await http.Downloadbook(
                        currentUser.Session,
                        keyNode.DeviceId,
                        chapterId
                    );
                    bookStream.Seek(0, SeekOrigin.Begin);

                    byte[] cleanAudio = await _decryptAudioCallback?.Invoke(bookStream)!;

                    string savedPath =
                        await BookManagement.SaveMp3Async(
                            _bookItem.Title ?? "",
                            cleanAudio,
                            chapterId
                        ) ?? throw new Exception();

                    lock (AvailableAudioChapters)
                    {
                        AvailableAudioChapters = AvailableAudioChapters
                            .Append($"{chapterId}.mp3")
                            .ToArray();
                    }

                    //UpdateAudioInteface(chapterId, savedPath, 0f, _chapterCodeNameMap[chapterId]);
                    this._logger?.Out($"Downloaded {chapterId}");
                    this._audioResourceManager?.OnAudioResourceChanged(
                        this,
                        new ResourceManager.AudioResourceChangedEventArgs(
                            this._bookItem.Title ?? "#",
                            savedPath,
                            chapterId,
                            0f
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                this._logger?.Out(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

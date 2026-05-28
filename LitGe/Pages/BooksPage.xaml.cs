using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LitGe.Lib;
using LitGe.Lib.Services;
//using LLibrary.Guards;
using SharedLib;
using ResourceManager = LitGe.Lib.ResourceManager.ResourceManager;

namespace LitGe.Pages
{
    public partial class BooksPage : ContentPage
    {
        private BooksPageViewModel? _viewModel;
        private readonly ResourceManager _resourceManager;
        private StateMachine<BookItem> _stateMachine;
        private string _username = Preferences.Default.Get("CurrentUser", "");

        private bool runOnce = false;
        private bool _isOpeningBook = false;

        public BooksPage()
        {
            InitializeComponent();
            //NavigationPage.SetHasNavigationBar(this, false);
            _resourceManager = /*Guard.AgainstNull(*/Provider.GetService<ResourceManager>()/*)*/;
            _stateMachine = new StateMachine<BookItem>(
                Path.Combine(FileSystem.Current.AppDataDirectory, $"{_username}.db")
            );
            _viewModel = new BooksPageViewModel(_stateMachine, _resourceManager);
            BindingContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            string currentUser = Preferences.Default.Get("CurrentUser", "");
            Console.WriteLine($"DEBUG_BOOKS_PAGE: OnNavigatedTo called. User: '{currentUser}', AlreadyRun: {runOnce}");

            // If the user has changed (e.g. after login), we MUST re-initialize everything
            if (currentUser != _username)
            {
                Console.WriteLine($"DEBUG_BOOKS_PAGE: User changed from '{_username}' to '{currentUser}'. Resetting state machine.");
                _username = currentUser;
                _stateMachine = new StateMachine<BookItem>(
                    Path.Combine(FileSystem.Current.AppDataDirectory, $"{_username}.db")
                );
                _viewModel = new BooksPageViewModel(_stateMachine, _resourceManager);
                BindingContext = _viewModel;
                runOnce = false; // Force re-load for new user
            }

            if (!runOnce)
            {
                runOnce = true;
                Console.WriteLine("DEBUG_BOOKS_PAGE: Starting first-time load for this session/user.");
                
                Task.Run(async () =>
                {
                    try 
                    {
                        await _stateMachine.InitAsync();
                        MainThread.BeginInvokeOnMainThread(() => {
                            Console.WriteLine("DEBUG_BOOKS_PAGE: Triggering LoadCollection.");
                            _viewModel?.LoadCollection();
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DEBUG_BOOKS_PAGE_ERROR: StateMachine init failed: {ex.Message}");
                    }
                });
            }
            else if (_viewModel != null && _viewModel.Books.Count == 0 && !_viewModel.Empty)
            {
                // Fallback for cases where it ran once but failed to load or was interrupted
                Console.WriteLine("DEBUG_BOOKS_PAGE: Retrying load (list empty).");
                _viewModel.LoadCollection();
            }
        }

        private async void search_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new BooksSeachPage(), true);
        }

        private void filter_Clicked(object sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_viewModel.State == BOOKS_PAGE_STATES.Horizontal)
                {
                    filter.Source = "filter";
                    _viewModel.State = BOOKS_PAGE_STATES.Vertical;
                }
                else
                {
                    _viewModel.State = BOOKS_PAGE_STATES.Horizontal;
                    filter.Source = "grid_icon";
                }
            });
        }

        //[Obsolete]
        //private async void bookListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    try
        //    {
        //        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not BookItem book)
        //            return;
        //        book.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        //        await _stateMachine.InsertRecordAsync(book);

        //        ((CollectionView)sender).SelectedItem = null;

        //        ModalReader mr = new ModalReader(book);
        //        await Shell.Current.Navigation.PushModalAsync(mr, true);
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine(
        //            $"Unhandled Exception: {ex.Message}\n{ex.StackTrace}"
        //        );
        //    }
        //}

        private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new BooksSeachPage(), true);
        }

        private void delete_icon_Clicked(object sender, EventArgs e)
        {
            // [BUG] roca srulad chamotvirtavs AUDIO wigns da mere washlas gaaketebs garedan
            // resource manageri itovebs progress files da hgonia ro tavebi gadmowerilia
            // sachiroa srulad gasuftavdes eg nawilic
            // axla ase iyos, tu agmoachenen mag bugs mere gavasworeb
            if (
                sender is Image button
                && button.BindingContext is BookItem selectedBook
                && selectedBook.IsDownloaded
            )
            {
                selectedBook.NeedDownload = true;
                selectedBook.IsDownloaded = false;
                _resourceManager.OnBookDeleteRequested(
                    this,
                    new ResourceManager.BookDeleteRequestEventArgs(book: selectedBook)
                );
            }
        }

        private void download_icon_Clicked(object sender, EventArgs e)
        {
            if (
                sender is Image button
                && button.BindingContext is BookItem selectedBook
                && selectedBook.NeedDownload
            )
            {
                selectedBook.NeedDownload = false;
                selectedBook.IsDownloaded = true;
                _resourceManager.OnBookDownloadRequested(
                    this,
                    new ResourceManager.BookDownloadRequestEventArgs(book: selectedBook)
                );
            }
        }

        private async void book_selected(object sender, EventArgs e)
        {
            if (_isOpeningBook) return;
            _isOpeningBook = true;

            BookItem bookItem;

            if (sender is Image button && button.BindingContext is BookItem selectedBook)
            {
                bookItem = selectedBook;
            }
            else if (
                sender is VerticalStackLayout vsl
                && vsl.BindingContext is BookItem selectedBook2
            )
            {
                bookItem = selectedBook2;
            }
            else
            {
                _isOpeningBook = false;
                return;
            }

            try
            {
                if (bookItem.NeedDownload)
                {
                    bookItem.NeedDownload = false;
                    bookItem.IsDownloaded = true;
                }

                bookItem.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Proactive deduplication: check if already in DB before inserting
                var existingBooks = await _stateMachine.RetrieveAsync();
                var found = existingBooks.FirstOrDefault(b =>
                    (string.IsNullOrEmpty(b.ProductId) && b.Title == bookItem.Title) ||
                    (!string.IsNullOrEmpty(b.ProductId) && b.ProductId == bookItem.ProductId));

                if (found != null)
                {
                    // Use UpdateAsync to preserve the record but refresh metadata/timestamp
                    await _stateMachine.UpdateAsync(bookItem);
                }
                else
                {
                    await _stateMachine.InsertAsync(bookItem);
                }

                ModalReader mr = new ModalReader(bookItem);
                await Shell.Current.Navigation.PushModalAsync(mr, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _isOpeningBook = false;
            }
        }
    }

    public class BooksPageViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<BookItem> Books { get; set; } =
            new ObservableCollection<BookItem>();

        public ICommand RefreshCommand =>
            new Command(() =>
            {
                if (_statemachine != null)
                {
                    LoadCollection();
                }
            });

        public Command BuyOnlineCommand =>
            new Command(async () =>
            {
                Uri uri = new Uri("https://www.lit.ge");
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            });

        private bool _arrangedVertically = false;
        private bool _arrangedHorizontally = true;

        private readonly BookManagement _bookManagement = new BookManagement();
        private readonly StateMachine<BookItem> _statemachine;
        private readonly ResourceManager _resourceManager;
        private bool _isLoading = false;
        private bool _isRefreshing = false;

        private bool _empty = false;

        public bool Empty
        {
            get => _empty;
            set
            {
                _empty = value;
                OnPropertyChanged();
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                _isRefreshing = value;
                OnPropertyChanged();
            }
        }

        public bool ArrangedVertically
        {
            get => _arrangedVertically;
            set
            {
                _arrangedVertically = value;
                OnPropertyChanged();
            }
        }

        public bool ArrangedHorizontally
        {
            get => _arrangedHorizontally;
            set
            {
                _arrangedHorizontally = value;
                OnPropertyChanged();
            }
        }

        private BOOKS_PAGE_STATES _state = BOOKS_PAGE_STATES.Loading;
        public BOOKS_PAGE_STATES State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
            }
        }
        private int[] _loadingMockContainer = new int[5];
        public int[] LoadingMockContainer
        {
            get => _loadingMockContainer;
            set
            {
                _loadingMockContainer = value;
                OnPropertyChanged();
            }
        }

        public BooksPageViewModel(
            StateMachine<BookItem> stateMachine,
            ResourceManager resourceManager
        )
        {
            _statemachine = stateMachine;
            _resourceManager = resourceManager;
            _statemachine.StateChanged += _statemachine_StateChanged;
        }

        private void _statemachine_StateChanged(object? sender, BookItem e)
        {
            int indexOfthatBook = Books.IndexOf(e);
            if (indexOfthatBook > 0)
            {
                Books.RemoveAt(indexOfthatBook);
                Books.Insert(0, e);
            }
        }

        public async void LoadCollection()
        {
            if (_isLoading)
            {
                return;
            }

            try
            {
                State = BOOKS_PAGE_STATES.Loading;
                IsRefreshing = true;
                _isLoading = true;
                
                string[] availableBooks = [];
                List<BookItem> booksFromdb = [];
                List<BookItem> booksFromServer = [];

                await Task.Run(async () => {
                    availableBooks = await _resourceManager.AvailableBooks();
                    booksFromdb = [.. (await _statemachine.RetrieveAsync()).OrderByDescending(x => x.Timestamp)];
                    booksFromServer = await _bookManagement.BookItemsAsync();
                });

                // CLEANUP: Deduplicate existing DB entries more robustly
                var uniqueKeys = new HashSet<string>();
                var toDelete = new List<BookItem>();
                foreach (var b in booksFromdb)
                {
                    // Create a unique key based on ProductId or Title if ProductId is missing
                    string key = !string.IsNullOrEmpty(b.ProductId) 
                        ? $"ID:{b.ProductId.Trim().ToLowerInvariant()}" 
                        : $"TITLE:{b.Title?.Trim().ToLowerInvariant() ?? "unknown"}";

                    if (uniqueKeys.Contains(key)) 
                    {
                        toDelete.Add(b);
                    }
                    else 
                    {
                        uniqueKeys.Add(key);
                    }
                }

                if (toDelete.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEDUPLICATE] Cleaning up {toDelete.Count} duplicates from database.");
                    // TODO: Find correct signature for DeleteAsync.
                    // foreach (var b in toDelete) await _statemachine.DeleteAsync(b);
                    booksFromdb = booksFromdb.Where(b => !toDelete.Contains(b)).ToList();
                }

                HashSet<BookItem> previousBooks = new HashSet<BookItem>(new BookItemComparer());
                booksFromdb.ForEach(b => previousBooks.Add(b));
                booksFromServer.ForEach(book => previousBooks.Add(book));

                MainThread.BeginInvokeOnMainThread(() => {
                    Books.Clear();
                });

                if (previousBooks.Count == 0)
                {
                    MainThread.BeginInvokeOnMainThread(() => {
                        Empty = true;
                        State = BOOKS_PAGE_STATES.Empty;
                    });
                    return;
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() => Empty = false);
                }

                List<BookItem> sorted =
                [
                    .. previousBooks.OrderByDescending(book =>
                    {
                        DateTime time = DateTimeOffset
                            .FromUnixTimeMilliseconds(book.Timestamp)
                            .UtcDateTime;
                        DateTime parsed = DateTime.MinValue;
                        if (
                            !string.IsNullOrWhiteSpace(book.Date)
                            && DateTime.TryParse(book.Date, out DateTime dt)
                        )
                        {
                            parsed = dt.ToUniversalTime();
                        }
                        return parsed > time ? parsed : time;
                    }),
                ];

                // Calculate isDownloaded flag in background
                for (int i = 0; i < sorted.Count; i++)
                {
                    bool isDownloaded = availableBooks.Any(f => f.Contains(sorted[i].Title ?? "x#x"));
                    sorted[i].IsDownloaded = isDownloaded;
                    sorted[i].NeedDownload = !isDownloaded;
                }

                MainThread.BeginInvokeOnMainThread(() => {
                    Books.Clear();
                    foreach (var book in sorted)
                    {
                        Books.Add(book);
                    }
                });
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => {
                    _isLoading = false;
                    IsRefreshing = false;
                    State = BOOKS_PAGE_STATES.Horizontal;
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    public enum BOOKS_PAGE_STATES
    {
        Loading,
        Success,
        Vertical,
        Horizontal,
        Empty,
    }
}
